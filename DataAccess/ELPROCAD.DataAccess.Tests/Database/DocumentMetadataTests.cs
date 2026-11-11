using ELPROCAD.DataAccess.Contracts.MDBX.Documents;
using ELPROCAD.DataAccess.CRUD;
using ELPROCAD.DataAccess.Database;
using ELPROCAD.DataAccess.Models.MDBX.Documents;
using ELPROCAD.DataAccess.Repositories.MDBX;
using Xunit;

namespace ELPROCAD.DataAccess.Tests.Database
{
	public sealed class DocumentMetadataTests : IDisposable
	{
		private readonly string _directory = Path.Combine(
			Path.GetTempPath(),
			"ELPROCAD.DataAccess.Tests",
			Guid.NewGuid().ToString("N"));

		private readonly IMdbxRepository _repository;
		private readonly IDatabaseAccess _database;
		private readonly Read _read;
		private readonly Guid _projectId;
		private readonly Guid _fileFormatId;

		public DocumentMetadataTests()
		{
			IDataAccessInitializer initializer = new DataAccessInitializer();

			var pdsx = initializer.InitializePdsx(
				Path.Combine(_directory, "Project.pdsx"));

			var project = pdsx.Project.Create(
				"TEST-81346",
				_directory,
				"Document metadata tests");

			Assert.True(project.Validation.IsValid);

			_projectId = project.Value!.Id;
			_fileFormatId = pdsx.FileFormat.GetByExtension(".dwg")!.Id;

			var mdbxPath = Path.Combine(_directory, "Project.mdbx");

			_repository = initializer.InitializeMdbx(mdbxPath);

			_database = new DatabaseAccess(
				DatabaseConnectionStrings.Create(mdbxPath));

			_read = new Read(_database);
		}

		private IDocument AddDocument(string name)
		{
			var result = _repository.Document.Create(
				_projectId,
				_fileFormatId,
				name,
				Path.Combine(_directory, name + ".dwg"));

			Assert.True(result.Validation.IsValid);

			return result.Value!;
		}

		[Fact(DisplayName =	"A document stores model space and multiple paper spaces independently of sheets")]
		public void Spaces_ArePersistedWithoutCreatingSheets()
		{
			var document = AddDocument("Drawing01");

			_database.Execute(unitOfWork =>
			{
				unitOfWork.Save(new CadDocumentSpace
				{
					DocumentId = document.Id,
					Name = "Model",
					SpaceKind = CadSpaceKind.ModelSpace
				});

				unitOfWork.Save(new CadDocumentSpace
				{
					DocumentId = document.Id,
					Name = "Layout1",
					SpaceKind = CadSpaceKind.PaperSpace
				});

				unitOfWork.Save(new CadDocumentSpace
				{
					DocumentId = document.Id,
					Name = "Layout2",
					SpaceKind = CadSpaceKind.PaperSpace
				});

				unitOfWork.CommitChanges();
			});

			var spaces = _read.GetAll<CadDocumentSpace>(
				item => item.DocumentId == document.Id);

			Assert.Equal(3, spaces.Count);
			Assert.Single(
				spaces.Where(item => item.SpaceKind == CadSpaceKind.ModelSpace));

			Assert.Equal(
				2,
				spaces.Count(item => item.SpaceKind == CadSpaceKind.PaperSpace));

			Assert.All(spaces, item => Assert.NotNull(item.LoadedVersion));

			Assert.Empty(_repository.Sheet.GetByDocumentId(document.Id));
		}

		[Fact(DisplayName =	"Document summary properties preserve all supplied values")]
		public void Properties_RoundTrip_PreservesValues()
		{
			var document = AddDocument("Drawing01");

			var input = new DocumentProperties
			{
				DocumentId = document.Id,
				Title = "Control cabinet",
				Subject = "Electrical schematic",
				Author = "Engineer",
				Keywords = "control; cabinet",
				Comments = "First line\r\nSecond line",
				LastSavedBy = "Reviewer",
				RevisionNumber = "B.2",
				HyperlinkBase = @"C:\Projects\References"
			};

			_database.Execute(unitOfWork =>
			{
				unitOfWork.Save(input);
				unitOfWork.CommitChanges();
			});

			var stored = _read.GetById<DocumentProperties>(input.Id);

			Assert.NotNull(stored);
			Assert.Equal(document.Id, stored!.DocumentId);
			Assert.Equal(input.Title, stored.Title);
			Assert.Equal(input.Subject, stored.Subject);
			Assert.Equal(input.Author, stored.Author);
			Assert.Equal(input.Keywords, stored.Keywords);
			Assert.Equal(input.Comments, stored.Comments);
			Assert.Equal(input.LastSavedBy, stored.LastSavedBy);
			Assert.Equal(input.RevisionNumber, stored.RevisionNumber);
			Assert.Equal(input.HyperlinkBase, stored.HyperlinkBase);
			Assert.NotNull(stored.LoadedVersion);
		}

		[Fact(DisplayName =	"A document cannot have two summary-properties records")]
		public void Properties_DuplicateDocument_IsRejected()
		{
			var document = AddDocument("Drawing01");

			_database.Execute(unitOfWork =>
			{
				unitOfWork.Save(new DocumentProperties
				{
					DocumentId = document.Id,
					Title = "Original"
				});

				unitOfWork.CommitChanges();
			});

			var exception = Record.Exception(() =>
				_database.Execute(unitOfWork =>
				{
					unitOfWork.Save(new DocumentProperties
					{
						DocumentId = document.Id,
						Title = "Duplicate"
					});

					unitOfWork.CommitChanges();
				}));

			Assert.True(
				VistaDbErrors.IsDuplicateKey(exception),
				$"Expected a duplicate-key error, but received: {exception}");

			var stored = Assert.Single(
				_read.GetAll<DocumentProperties>(
					item => item.DocumentId == document.Id));

			Assert.Equal("Original", stored.Title);
		}

		[Fact(DisplayName =	"Custom property names can be reused across different documents")]
		public void CustomProperties_SameNameAcrossDocuments_IsAllowed()
		{
			var first = AddDocument("First");
			var second = AddDocument("Second");
			var longValue = new string('X', 4096);

			_database.Execute(unitOfWork =>
			{
				unitOfWork.Save(new CadDocumentCustomProperty
				{
					DocumentId = first.Id,
					Name = "Customer",
					Value = longValue
				});

				unitOfWork.Save(new CadDocumentCustomProperty
				{
					DocumentId = second.Id,
					Name = "Customer",
					Value = "Another customer"
				});

				unitOfWork.CommitChanges();
			});

			var firstProperty = Assert.Single(
				_read.GetAll<CadDocumentCustomProperty>(
					item => item.DocumentId == first.Id));

			var secondProperty = Assert.Single(
				_read.GetAll<CadDocumentCustomProperty>(
					item => item.DocumentId == second.Id));

			Assert.Equal(longValue, firstProperty.Value);
			Assert.Equal("Another customer", secondProperty.Value);
		}

		[Fact(DisplayName =	"Duplicate custom property names within a document are rejected")]
		public void CustomProperties_DuplicateName_IsRejected()
		{
			var document = AddDocument("Drawing01");

			_database.Execute(unitOfWork =>
			{
				unitOfWork.Save(new CadDocumentCustomProperty
				{
					DocumentId = document.Id,
					Name = "Customer",
					Value = "Original"
				});

				unitOfWork.CommitChanges();
			});

			var exception = Record.Exception(() =>
				_database.Execute(unitOfWork =>
				{
					unitOfWork.Save(new CadDocumentCustomProperty
					{
						DocumentId = document.Id,
						Name = "Customer",
						Value = "Duplicate"
					});

					unitOfWork.CommitChanges();
				}));

			Assert.True(
				VistaDbErrors.IsDuplicateKey(exception),
				$"Expected a duplicate-key error, but received: {exception}");

			var stored = Assert.Single(
				_read.GetAll<CadDocumentCustomProperty>(
					item => item.DocumentId == document.Id));

			Assert.Equal("Original", stored.Value);
		}

		[Theory(DisplayName = "Stored spaces or properties prevent document deletion")]
		[InlineData("Space")]
		[InlineData("Properties")]
		[InlineData("CustomProperty")]
		public void Delete_DocumentWithMetadata_IsRejected(string recordType)
		{
			var document = AddDocument("Drawing01");

			_database.Execute(unitOfWork =>
			{
				switch (recordType)
				{
					case "Space":
						unitOfWork.Save(new CadDocumentSpace
						{
							DocumentId = document.Id,
							Name = "Model",
							SpaceKind = CadSpaceKind.ModelSpace
						});
						break;

					case "Properties":
						unitOfWork.Save(new DocumentProperties
						{
							DocumentId = document.Id
						});
						break;

					case "CustomProperty":
						unitOfWork.Save(new CadDocumentCustomProperty
						{
							DocumentId = document.Id,
							Name = "Customer",
							Value = "Example"
						});
						break;
				}

				unitOfWork.CommitChanges();
			});

			var result = _repository.Document.Delete(document);

			Assert.False(result.IsValid);
			Assert.NotNull(_repository.Document.GetById(document.Id));
		}

		[Fact(DisplayName =	"Metadata repositories return only the requested document's data")]
		public void Repositories_FilterByDocument_AndReturnOrderedData()
		{
			var first = AddDocument("First");
			var second = AddDocument("Second");

			_database.Execute(unitOfWork =>
			{
				unitOfWork.Save(new CadDocumentSpace
				{
					DocumentId = first.Id,
					Name = "ZModel",
					SpaceKind = CadSpaceKind.ModelSpace
				});

				unitOfWork.Save(new CadDocumentSpace
				{
					DocumentId = first.Id,
					Name = "Layout2",
					SpaceKind = CadSpaceKind.PaperSpace
				});

				unitOfWork.Save(new CadDocumentSpace
				{
					DocumentId = first.Id,
					Name = "Layout1",
					SpaceKind = CadSpaceKind.PaperSpace
				});

				unitOfWork.Save(new CadDocumentSpace
				{
					DocumentId = second.Id,
					Name = "OtherModel",
					SpaceKind = CadSpaceKind.ModelSpace
				});

				unitOfWork.Save(new DocumentProperties
				{
					DocumentId = first.Id,
					Title = "First document"
				});

				unitOfWork.Save(new DocumentProperties
				{
					DocumentId = second.Id,
					Title = "Second document"
				});

				unitOfWork.Save(new CadDocumentCustomProperty
				{
					DocumentId = first.Id,
					Name = "Project",
					Value = "P-001"
				});

				unitOfWork.Save(new CadDocumentCustomProperty
				{
					DocumentId = first.Id,
					Name = "Customer",
					Value = "First customer"
				});

				unitOfWork.Save(new CadDocumentCustomProperty
				{
					DocumentId = second.Id,
					Name = "Customer",
					Value = "Second customer"
				});

				unitOfWork.CommitChanges();
			});

			var spaces = _repository.CadDocumentSpace.GetByDocumentId(first.Id);

			Assert.Equal(
				new[] { "ZModel", "Layout1", "Layout2" },
				spaces.Select(space => space.Name));

			Assert.All(spaces, space =>
			{
				Assert.Equal(first.Id, space.DocumentId);
				Assert.NotNull(space.LoadedVersion);
			});

			var properties = _repository.DocumentProperties.GetByDocumentId(first.Id);

			Assert.NotNull(properties);
			Assert.Equal("First document", properties!.Title);
			Assert.NotNull(properties.LoadedVersion);

			var customProperties =
				_repository.CadDocumentCustomProperty.GetByDocumentId(first.Id);

			Assert.Equal(
				new[] { "Customer", "Project" },
				customProperties.Select(property => property.Name));

			Assert.Equal("First customer", customProperties[0].Value);

			Assert.All(customProperties, property =>
			{
				Assert.Equal(first.Id, property.DocumentId);
				Assert.NotNull(property.LoadedVersion);
			});

			Assert.NotNull(
				_repository.CadDocumentSpace.GetById(spaces[0].Id));

			Assert.NotNull(
				_repository.CadDocumentCustomProperty.GetById(customProperties[0].Id));
		}

		[Fact(DisplayName =	"Documents without stored metadata return empty collections and no summary")]
		public void Repositories_DocumentWithoutMetadata_ReturnEmptyResults()
		{
			var document = AddDocument("Empty");

			Assert.Empty(
				_repository.CadDocumentSpace.GetByDocumentId(document.Id));

			Assert.Null(
				_repository.DocumentProperties.GetByDocumentId(document.Id));

			Assert.Empty(
				_repository.CadDocumentCustomProperty.GetByDocumentId(document.Id));

			Assert.Null(
				_repository.CadDocumentSpace.GetById(Guid.NewGuid()));

			Assert.Null(
				_repository.CadDocumentCustomProperty.GetById(Guid.NewGuid()));
		}

		[Fact(DisplayName =	"Editing returned document properties does not implicitly persist changes")]
		public void Read_Metadata_IsDetachedFromDatabase()
		{
			var document = AddDocument("Detached");

			_database.Execute(unitOfWork =>
			{
				unitOfWork.Save(new DocumentProperties
				{
					DocumentId = document.Id,
					Title = "Stored title"
				});

				unitOfWork.CommitChanges();
			});

			var properties =
				_repository.DocumentProperties.GetByDocumentId(document.Id)!;

			properties.Title = "Unsaved title";

			var stored =
				_repository.DocumentProperties.GetByDocumentId(document.Id)!;

			Assert.Equal("Stored title", stored.Title);
		}

		public void Dispose()
		{
			if (Directory.Exists(_directory))
				Directory.Delete(_directory, recursive: true);
		}
	}
}