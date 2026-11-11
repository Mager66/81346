using ELPROCAD.DataAccess.Contracts.MDBX.Documents;
using ELPROCAD.DataAccess.Repositories.MDBX;
using Xunit;

namespace ELPROCAD.DataAccess.Tests.Database
{
	public sealed class SheetTests : IDisposable
	{
		private readonly string _directory = Path.Combine(
			Path.GetTempPath(),
			"ELPROCAD.DataAccess.Tests",
			Guid.NewGuid().ToString("N"));

		private readonly IMdbxRepository _repository;
		private readonly Guid _projectId;
		private readonly Guid _fileFormatId;

		public SheetTests()
		{
			IDataAccessInitializer initializer = new DataAccessInitializer();

			var pdsx = initializer.InitializePdsx(
				Path.Combine(_directory, "Project.pdsx"));

			var project = pdsx.Project.Create(
				"TEST-81346",
				_directory,
				"Sheet tests");

			Assert.True(project.Validation.IsValid);

			_projectId = project.Value!.Id;
			_fileFormatId = pdsx.FileFormat.GetByExtension(".dwg")!.Id;

			_repository = initializer.InitializeMdbx(
				Path.Combine(_directory, "Project.mdbx"));
		}

		private IDocument AddDocument(string name)
		{
			var result = _repository.Document.Create(
				_projectId,
				_fileFormatId,
				name,
				Path.Combine(_directory, name + ".dwg"));

			Assert.True(result.Validation.IsValid);
			Assert.NotNull(result.Value);

			return result.Value!;
		}

		private ISheet AddSheet(Guid documentId, string sheetNo)
		{
			var result = _repository.Sheet.Create(documentId, sheetNo);

			Assert.True(result.Validation.IsValid);
			Assert.NotNull(result.Value);

			return result.Value!;
		}

		[Fact(DisplayName =	"Sheets are persisted and returned in order for their document")]
		public void CreateAndRead_PreservesDocumentAndSheetNumber()
		{
			var document = AddDocument("First");

			AddSheet(document.Id, "002");
			var first = AddSheet(document.Id, "001");

			var sheets = _repository.Sheet.GetByDocumentId(document.Id);

			Assert.Equal(
				new[] { "001", "002" },
				sheets.Select(sheet => sheet.SheetNo));

			var stored = _repository.Sheet.GetById(first.Id);

			Assert.NotNull(stored);
			Assert.Equal(document.Id, stored!.DocumentId);
			Assert.NotNull(stored.LoadedVersion);

			Assert.Empty(
				_repository.Sheet.GetByDocumentId(Guid.NewGuid()));
		}

		[Fact(DisplayName =	"The same sheet number is allowed in different documents")]
		public void Create_SameNumberInDifferentDocuments_IsAllowed()
		{
			var first = AddDocument("First");
			var second = AddDocument("Second");

			AddSheet(first.Id, "001");
			AddSheet(second.Id, "001");

			Assert.Single(_repository.Sheet.GetByDocumentId(first.Id));
			Assert.Single(_repository.Sheet.GetByDocumentId(second.Id));
		}

		[Fact(DisplayName =	"A duplicate sheet number within the same document is rejected")]
		public void Create_DuplicateNumberInDocument_IsRejected()
		{
			var document = AddDocument("First");
			var original = AddSheet(document.Id, "001");

			var result = _repository.Sheet.Create(document.Id, "001");

			Assert.Null(result.Value);
			Assert.False(result.Validation.IsValid);

			var stored = Assert.Single(
				_repository.Sheet.GetByDocumentId(document.Id));

			Assert.Equal(original.Id, stored.Id);
		}

		[Theory(DisplayName = "Invalid sheet numbers are rejected without inserting data")]
		[InlineData("")]
		[InlineData("   ")]
		[InlineData("12345678901")]
		public void Create_InvalidSheetNumber_IsRejected(string sheetNo)
		{
			var document = AddDocument("First");

			var result = _repository.Sheet.Create(document.Id, sheetNo);

			Assert.Null(result.Value);

			var error = Assert.Single(result.Validation.Errors);

			Assert.Equal(nameof(ISheet.SheetNo), error.PropertyName);
			Assert.Empty(_repository.Sheet.GetByDocumentId(document.Id));
		}

		[Fact(DisplayName =	"A sheet cannot be created for an unknown document")]
		public void Create_UnknownDocument_IsRejected()
		{
			var result = _repository.Sheet.Create(Guid.NewGuid(), "001");

			Assert.Null(result.Value);

			var error = Assert.Single(result.Validation.Errors);

			Assert.Equal(nameof(ISheet.DocumentId), error.PropertyName);
		}

		[Fact(DisplayName =	"Updating a sheet preserves its identity and document")]
		public void Update_ChangesNumberAndPreservesOwnership()
		{
			var document = AddDocument("First");
			var sheet = AddSheet(document.Id, "001");

			sheet.SheetNo = "002";

			var result = _repository.Sheet.Update(sheet);

			Assert.True(result.Validation.IsValid);
			Assert.Equal(sheet.Id, result.Value!.Id);
			Assert.Equal(document.Id, result.Value.DocumentId);

			Assert.Equal(
				"002",
				_repository.Sheet.GetById(sheet.Id)!.SheetNo);
		}

		[Fact(DisplayName =	"Updating to an existing sheet number preserves the original record")]
		public void Update_DuplicateNumber_IsRejected()
		{
			var document = AddDocument("First");

			AddSheet(document.Id, "001");
			var second = AddSheet(document.Id, "002");

			second.SheetNo = "001";

			var result = _repository.Sheet.Update(second);

			Assert.Null(result.Value);
			Assert.False(result.Validation.IsValid);

			Assert.Equal(
				"002",
				_repository.Sheet.GetById(second.Id)!.SheetNo);
		}

		[Fact(DisplayName =	"Outdated sheets cannot overwrite or delete a newer version")]
		public void UpdateAndDelete_OutdatedVersion_AreRejected()
		{
			var document = AddDocument("First");
			var created = AddSheet(document.Id, "001");

			var first = _repository.Sheet.GetById(created.Id)!;
			var outdated = _repository.Sheet.GetById(created.Id)!;

			first.SheetNo = "002";

			Assert.True(_repository.Sheet.Update(first).Validation.IsValid);

			outdated.SheetNo = "003";

			Assert.False(
				_repository.Sheet.Update(outdated).Validation.IsValid);

			Assert.False(_repository.Sheet.Delete(outdated).IsValid);

			Assert.Equal(
				"002",
				_repository.Sheet.GetById(created.Id)!.SheetNo);
		}

		[Fact(DisplayName =	"A document cannot be deleted until its sheets have been removed")]
		public void Delete_DocumentWithSheets_IsRejected()
		{
			var document = AddDocument("First");
			var sheet = AddSheet(document.Id, "001");

			var blocked = _repository.Document.Delete(document);

			Assert.False(blocked.IsValid);
			Assert.NotNull(_repository.Document.GetById(document.Id));
			Assert.NotNull(_repository.Sheet.GetById(sheet.Id));

			Assert.True(_repository.Sheet.Delete(sheet).IsValid);

			Assert.NotNull(_repository.Document.GetById(document.Id));
			Assert.True(_repository.Document.Delete(document).IsValid);

			Assert.Null(_repository.Document.GetById(document.Id));
		}

		public void Dispose()
		{
			if (Directory.Exists(_directory))
				Directory.Delete(_directory, recursive: true);
		}
	}
}
