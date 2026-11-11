using ELPROCAD.DataAccess.Contracts.MDBX.Documents;
using ELPROCAD.DataAccess.Database;
using ELPROCAD.DataAccess.Models.MDBX.Documents;
using ELPROCAD.DataAccess.Models.PDSX.Documents;
using ELPROCAD.DataAccess.Repositories.MDBX;
using Xunit;

namespace ELPROCAD.DataAccess.Tests.Database
{
	public sealed class MdbxDocumentTests : IDisposable
	{
		private readonly string _directory = Path.Combine(
			Path.GetTempPath(),
			"ELPROCAD.DataAccess.Tests",
			Guid.NewGuid().ToString("N"));

		private readonly IDataAccessInitializer _initializer =
			new DataAccessInitializer();

		private string MdbxPath => Path.Combine(_directory, "Project.mdbx");

		private IMdbxRepository OpenMdbx()
		{
			return _initializer.InitializeMdbx(MdbxPath);
		}

		[Theory(DisplayName = "MDBX documents reference a PDSX project by identifier regardless of file type")]
		[InlineData("Drawing01.dwg")]
		[InlineData("Specification.pdf")]
		public void Create_ReferencesPdsxProject_AndPersistsDocument(string fileName)
		{
			var pdsx = _initializer.InitializePdsx(
				Path.Combine(_directory, "Project.pdsx"));

			var projectResult = pdsx.Project.Create(
				"TEST-81346",
				_directory,
				"Document ownership test");

			Assert.True(projectResult.Validation.IsValid);
			Assert.NotNull(projectResult.Value);

			var project = projectResult.Value!;
			var mdbx = OpenMdbx();
			var documentPath = Path.Combine(_directory, fileName);

			var created = mdbx.Document.Create(
				project.Id,
				Guid.NewGuid(),
				fileName,
				documentPath);

			Assert.True(created.Validation.IsValid);
			Assert.NotNull(created.Value);
			Assert.NotNull(created.Value!.LoadedVersion);

			// Reinitialization must preserve the stored document.
			var reopened = OpenMdbx();
			var stored = reopened.Document.GetById(created.Value.Id);

			Assert.NotNull(stored);
			Assert.Equal(project.Id, stored!.ProjectId);
			Assert.Equal(fileName, stored.Name);
			Assert.Equal(documentPath, stored.Path);
			Assert.NotNull(stored.LoadedVersion);

			// Resolve the cross-database relationship explicitly.
			Assert.NotNull(pdsx.Project.GetById(stored.ProjectId));

			var listed = Assert.Single(reopened.Document.GetAll());
			Assert.Equal(stored.Id, listed.Id);

			// Editing returned data must not persist implicitly.
			stored.Name = "Unsaved change";

			Assert.Equal(
				fileName,
				reopened.Document.GetById(stored.Id)!.Name);
		}

		[Fact(DisplayName =	"Creating a document without a project identifier is rejected")]
		public void Create_EmptyProjectId_IsRejected()
		{
			var mdbx = OpenMdbx();

			var result = mdbx.Document.Create(
				Guid.Empty,
				Guid.Empty,
				"Drawing01",
				Path.Combine(_directory, "Drawing01.dwg"));

			Assert.Null(result.Value);
			Assert.False(result.Validation.IsValid);

			Assert.Equal(2, result.Validation.Errors.Count);
			Assert.Contains(result.Validation.Errors, error => error.PropertyName == nameof(IDocument.ProjectId));
			Assert.Contains(result.Validation.Errors, error => error.PropertyName==nameof(IDocument.FileFormatId));

			Assert.Empty(mdbx.Document.GetAll());
		}

		[Theory(DisplayName = "Invalid document metadata is rejected without inserting data")]
		[InlineData("Name", 0)]
		[InlineData("Name", 51)]
		[InlineData("Path", 0)]
		[InlineData("Path", 256)]
		public void Create_InvalidMetadata_IsRejected(string propertyName, int length)
		{
			var mdbx = OpenMdbx();
			var invalidValue = new string('A', length);

			var result = mdbx.Document.Create(
				Guid.NewGuid(),
				Guid.NewGuid(),
				propertyName == "Name" ? invalidValue : "Drawing01",
				propertyName == "Path"
					? invalidValue
					: Path.Combine(_directory, "Drawing01.dwg"));

			Assert.Null(result.Value);
			Assert.False(result.Validation.IsValid);

			var error = Assert.Single(result.Validation.Errors);

			Assert.Equal(propertyName, error.PropertyName);
			Assert.Empty(mdbx.Document.GetAll());
		}

		[Fact(DisplayName =	"Reading an unknown document identifier returns null")]
		public void GetById_UnknownIdentifier_ReturnsNull()
		{
			Assert.Null(
				OpenMdbx().Document.GetById(Guid.NewGuid()));
		}

		[Fact(DisplayName =	"MDBX repositories remain isolated between project databases")]
		public void SeparateDatabases_DoNotShareDocuments()
		{
			var first = OpenMdbx();

			var second = _initializer.InitializeMdbx(
				Path.Combine(_directory, "OtherProject.mdbx"));

			var created = first.Document.Create(
				Guid.NewGuid(),
				Guid.NewGuid(),
				"Drawing01",
				Path.Combine(_directory, "Drawing01.dwg"));

			Assert.True(created.Validation.IsValid);

			Assert.Single(first.Document.GetAll());
			Assert.Empty(second.Document.GetAll());
		}

		#region Update

		[Fact(DisplayName = "Updating a document persists metadata and preserves project ownership")]
		public void Update_ValidDocument_PersistsChanges()
		{
			var repository = OpenMdbx();
			var projectId = Guid.NewGuid();

			var created = repository.Document.Create(
				projectId,
				Guid.NewGuid(),
				"Original",
				Path.Combine(_directory, "Original.dwg"));

			Assert.True(created.Validation.IsValid);
			Assert.NotNull(created.Value);

			var document = created.Value!;
			var originalVersion = document.LoadedVersion;

			document.Name = "Updated";
			document.Path = Path.Combine(_directory, "Updated.dwg");

			var result = repository.Document.Update(document);

			Assert.True(result.Validation.IsValid);
			Assert.NotNull(result.Value);
			Assert.NotEqual(originalVersion, result.Value!.LoadedVersion);

			var stored = OpenMdbx().Document.GetById(document.Id);

			Assert.NotNull(stored);
			Assert.Equal(document.Id, stored!.Id);
			Assert.Equal(projectId, stored.ProjectId);
			Assert.Equal("Updated", stored.Name);
			Assert.Equal(document.Path, stored.Path);

			// The returned version must support another update.
			result.Value.Name = "Updated again";

			var second = repository.Document.Update(result.Value);

			Assert.True(second.Validation.IsValid);
			Assert.Equal(
				"Updated again",
				OpenMdbx().Document.GetById(document.Id)!.Name);
		}

		[Theory(DisplayName = "Invalid document updates leave stored metadata unchanged")]
		[InlineData("Name")]
		[InlineData("Path")]
		public void Update_InvalidMetadata_PreservesStoredData(string propertyName)
		{
			var repository = OpenMdbx();
			var originalPath = Path.Combine(_directory, "Original.dwg");

			var created = repository.Document.Create(
				Guid.NewGuid(),
				Guid.NewGuid(),
				"Original",
				originalPath);

			Assert.True(created.Validation.IsValid);

			var document = created.Value!;

			if (propertyName == "Name")
				document.Name = "";
			else
				document.Path = "";

			var result = repository.Document.Update(document);

			Assert.Null(result.Value);
			Assert.False(result.Validation.IsValid);

			var error = Assert.Single(result.Validation.Errors);

			Assert.Equal(propertyName, error.PropertyName);

			var stored = repository.Document.GetById(document.Id);

			Assert.NotNull(stored);
			Assert.Equal("Original", stored!.Name);
			Assert.Equal(originalPath, stored.Path);
		}

		[Fact(DisplayName =	"An outdated document cannot overwrite another saved change")]
		public void Update_OutdatedVersion_IsRejected()
		{
			var repository = OpenMdbx();

			var created = repository.Document.Create(
				Guid.NewGuid(),
				Guid.NewGuid(),
				"Original",
				Path.Combine(_directory, "Original.dwg"));

			Assert.True(created.Validation.IsValid);

			var id = created.Value!.Id;

			var first = repository.Document.GetById(id)!;
			var second = Assert.Single(repository.Document.GetAll());

			Assert.NotNull(first.LoadedVersion);
			Assert.Equal(first.LoadedVersion, second.LoadedVersion);

			first.Name = "First change";

			Assert.True(repository.Document.Update(first).Validation.IsValid);

			second.Name = "Second change";

			var result = repository.Document.Update(second);

			Assert.Null(result.Value);
			Assert.False(result.Validation.IsValid);

			Assert.Equal(
				"First change",
				repository.Document.GetById(id)!.Name);

			// The caller retains its unsaved input after a conflict.
			Assert.Equal("Second change", second.Name);
		}

		[Fact(DisplayName =	"A document update without its original version is rejected")]
		public void Update_MissingVersion_IsRejected()
		{
			var repository = OpenMdbx();

			var created = repository.Document.Create(
				Guid.NewGuid(),
				Guid.NewGuid(),
				"Original",
				Path.Combine(_directory, "Original.dwg"));

			Assert.True(created.Validation.IsValid);

			var original = created.Value!;

			var input = new Document
			{
				Id = original.Id,
				ProjectId = original.ProjectId,
				Name = "Changed",
				Path = original.Path
			};

			var result = repository.Document.Update(input);

			Assert.Null(result.Value);
			Assert.False(result.Validation.IsValid);

			Assert.Equal(
				"Original",
				repository.Document.GetById(original.Id)!.Name);
		}

		[Fact(DisplayName =	"Updating a missing document returns an identifier validation error")]
		public void Update_MissingDocument_IsRejected()
		{
			var repository = OpenMdbx();

			var input = new Document
			{
				Id = Guid.NewGuid(),
				ProjectId = Guid.NewGuid(),
				FileFormatId = Guid.NewGuid(),
				Name = "Missing",
				Path = Path.Combine(_directory, "Missing.dwg"),
				LoadedVersion = 0
			};

			var result = repository.Document.Update(input);

			Assert.Null(result.Value);

			var error = Assert.Single(result.Validation.Errors);

			Assert.Equal(nameof(IDocument.Id), error.PropertyName);
			Assert.Empty(repository.Document.GetAll());
		}

		#endregion Update

		#region Delete

		[Fact(DisplayName =	"Deleting a document removes its record without deleting the physical file")]
		public void Delete_CurrentDocument_RemovesOnlyDatabaseRecord()
		{
			var repository = OpenMdbx();
			var documentPath = Path.Combine(_directory, "Document.txt");

			File.WriteAllText(documentPath, "Physical document content.");

			var created = repository.Document.Create(
				Guid.NewGuid(),
				Guid.NewGuid(),
				"Document",
				documentPath);

			Assert.True(created.Validation.IsValid);

			var document = repository.Document.GetById(created.Value!.Id)!;

			var result = repository.Document.Delete(document);

			Assert.True(result.IsValid);

			var reopened = OpenMdbx();

			Assert.Null(reopened.Document.GetById(document.Id));
			Assert.Empty(reopened.Document.GetAll());

			Assert.True(File.Exists(documentPath));
			Assert.Equal(
				"Physical document content.",
				File.ReadAllText(documentPath));
		}

		[Fact(DisplayName =	"An outdated document version cannot delete a subsequently updated record")]
		public void Delete_OutdatedVersion_PreservesUpdatedDocument()
		{
			var repository = OpenMdbx();

			var created = repository.Document.Create(
				Guid.NewGuid(),
				Guid.NewGuid(),
				"Original",
				Path.Combine(_directory, "Original.dwg"));

			Assert.True(created.Validation.IsValid);

			var id = created.Value!.Id;
			var outdated = repository.Document.GetById(id)!;
			var current = repository.Document.GetById(id)!;

			current.Name = "Updated";

			Assert.True(repository.Document.Update(current).Validation.IsValid);

			var result = repository.Document.Delete(outdated);

			Assert.False(result.IsValid);

			var stored = repository.Document.GetById(id);

			Assert.NotNull(stored);
			Assert.Equal("Updated", stored!.Name);
		}

		[Fact(DisplayName =	"Deleting a document without its original version is rejected")]
		public void Delete_MissingVersion_PreservesDocument()
		{
			var repository = OpenMdbx();

			var created = repository.Document.Create(
				Guid.NewGuid(),
				Guid.NewGuid(),
				"Original",
				Path.Combine(_directory, "Original.dwg"));

			Assert.True(created.Validation.IsValid);

			var original = created.Value!;

			var input = new Document
			{
				Id = original.Id,
				ProjectId = original.ProjectId,
				Name = original.Name,
				Path = original.Path
			};

			var result = repository.Document.Delete(input);

			Assert.False(result.IsValid);
			Assert.NotNull(repository.Document.GetById(original.Id));
		}

		[Fact(DisplayName =	"Deleting an already deleted document returns an identifier validation error")]
		public void Delete_AlreadyDeletedDocument_ReturnsFailure()
		{
			var repository = OpenMdbx();

			var created = repository.Document.Create(
				Guid.NewGuid(),
				Guid.NewGuid(),
				"Document",
				Path.Combine(_directory, "Document.dwg"));

			Assert.True(created.Validation.IsValid);

			var document = created.Value!;

			Assert.True(repository.Document.Delete(document).IsValid);

			var result = repository.Document.Delete(document);

			Assert.False(result.IsValid);

			var error = Assert.Single(result.Errors);

			Assert.Equal(nameof(IDocument.Id), error.PropertyName);
			Assert.Empty(repository.Document.GetAll());
		}

		[Fact(DisplayName =	"Deleting a document does not require valid editable metadata")]
		public void Delete_InvalidLocalMetadata_StillDeletesCurrentRecord()
		{
			var repository = OpenMdbx();

			var created = repository.Document.Create(
				Guid.NewGuid(),
				Guid.NewGuid(),
				"Document",
				Path.Combine(_directory, "Document.dwg"));

			Assert.True(created.Validation.IsValid);

			var document = created.Value!;

			// Deletion depends on identity and version, not unsaved field values.
			document.Name = "";
			document.Path = "";

			var result = repository.Document.Delete(document);

			Assert.True(result.IsValid);
			Assert.Null(repository.Document.GetById(document.Id));
		}

		#endregion Delete

		#region Document kind

		[Fact(DisplayName =	"A document references a document kind stored in the project's PDSX database")]
		public void Create_DocumentKindReference_CanBeResolvedInPdsx()
		{
			var pdsxPath = Path.Combine(_directory, "Project.pdsx");
			var pdsx = _initializer.InitializePdsx(pdsxPath);

			var kindId = Guid.NewGuid();

			var configurationDatabase = new DatabaseAccess(
				DatabaseConnectionStrings.Create(pdsxPath));

			// Populate configuration internally for this integration test.
			configurationDatabase.Execute(unitOfWork =>
			{
				unitOfWork.Save(new FileFormat
				{
					Id = kindId,
					Name = "Electrical schematic",
					Description = "Electrical circuit documentation"
				});

				unitOfWork.CommitChanges();
			});

			var projectResult = pdsx.Project.Create(
				"TEST-81346",
				_directory,
				"Document kind test");

			Assert.True(projectResult.Validation.IsValid);

			var mdbx = OpenMdbx();

			var created = mdbx.Document.Create(
				projectResult.Value!.Id,
				kindId,
				"Drawing01",
				Path.Combine(_directory, "Drawing01.dwg"));

			Assert.True(created.Validation.IsValid);

			var stored = OpenMdbx().Document.GetById(created.Value!.Id);

			Assert.NotNull(stored);
			Assert.Equal(kindId, stored!.FileFormatId);

			var kind = pdsx.FileFormat.GetById(stored.FileFormatId);

			Assert.NotNull(kind);
			Assert.Equal("Electrical schematic", kind!.Name);
			Assert.Equal("Electrical circuit documentation", kind.Description);

			// Metadata updates must preserve the document kind.
			stored.Name = "Renamed drawing";

			var updated = mdbx.Document.Update(stored);

			Assert.True(updated.Validation.IsValid);
			Assert.Equal(kindId, updated.Value!.FileFormatId);
			Assert.Equal(
				kindId,
				OpenMdbx().Document.GetById(stored.Id)!.FileFormatId);
		}

		[Fact(DisplayName =	"Creating a document without a document kind is rejected")]
		public void Create_MissingDocumentKind_IsRejected()
		{
			var repository = OpenMdbx();

			var result = repository.Document.Create(
				Guid.NewGuid(),
				Guid.Empty,
				"Drawing01",
				Path.Combine(_directory, "Drawing01.dwg"));

			Assert.Null(result.Value);
			Assert.False(result.Validation.IsValid);

			var error = Assert.Single(result.Validation.Errors);

			Assert.Equal(nameof(IDocument.FileFormatId), error.PropertyName);
			Assert.Empty(repository.Document.GetAll());
		}

		[Fact(DisplayName =	"Reinitializing PDSX preserves custom file formats and returns an ordered lookup")]
		public void FileFormats_Reinitialize_PreservesOrderedLookup()
		{
			var path = Path.Combine(_directory, "Formats.pdsx");
			var repository = _initializer.InitializePdsx(path);

			var initialCount = repository.FileFormat.GetAll().Count;

			var first = repository.FileFormat.Create(
				"Zeta format",
				"First custom format",
				".zeta");

			var second = repository.FileFormat.Create(
				"Alpha format",
				"Second custom format",
				".alpha");

			Assert.True(first.Validation.IsValid);
			Assert.True(second.Validation.IsValid);

			var reopened = _initializer.InitializePdsx(path);
			var formats = reopened.FileFormat.GetAll();

			Assert.Equal(initialCount + 2, formats.Count);

			Assert.Contains(formats, format =>
				format.Id == first.Value!.Id &&
				format.Name == "Zeta format" &&
				format.Extension == ".zeta");

			Assert.Contains(formats, format =>
				format.Id == second.Value!.Id &&
				format.Name == "Alpha format" &&
				format.Extension == ".alpha");

			var names = formats.Select(format => format.Name).ToArray();

			Assert.Equal(
				names.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray(),
				names);

			Assert.Null(reopened.FileFormat.GetById(Guid.NewGuid()));
		}

		#endregion Document kind

		public void Dispose()
		{
			// Each test owns its temporary database directory.
			if (Directory.Exists(_directory))
				Directory.Delete(_directory, recursive: true);
		}
	}
}
