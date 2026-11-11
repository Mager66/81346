using ELPROCAD.DataAccess.Contracts.PDSX.Documents;
using ELPROCAD.DataAccess.Repositories.PDSX;
using Xunit;

namespace ELPROCAD.DataAccess.Tests.Database
{
	public sealed class FileFormatTests : IDisposable
	{
		private readonly string _directory = Path.Combine(
			Path.GetTempPath(),
			"ELPROCAD.DataAccess.Tests",
			Guid.NewGuid().ToString("N"));

		private readonly IDataAccessInitializer _initializer =
			new DataAccessInitializer();

		private string DatabasePath =>
			Path.Combine(_directory, "Project.pdsx");

		private IPdsxRepository Open()
		{
			return _initializer.InitializePdsx(DatabasePath);
		}

		[Fact(DisplayName =	"A new PDSX database contains the default file formats")]
		public void Initialize_NewDatabase_AddsDefaultFormats()
		{
			var formats = Open().FileFormat.GetAll();

			Assert.Equal(
				new[] { ".docx", ".dwg", ".dxf", ".pdf", ".txt", ".xlsx" },
				formats.Select(format => format.Extension)
					.OrderBy(extension => extension));

			Assert.All(formats, format =>
			{
				Assert.NotEqual(Guid.Empty, format.Id);
				Assert.NotNull(format.LoadedVersion);
			});
		}

		[Fact(DisplayName =	"Reopening PDSX preserves modified defaults without adding duplicates")]
		public void Initialize_ExistingDatabase_PreservesUserChanges()
		{
			var repository = Open();
			var originalCount = repository.FileFormat.GetAll().Count;

			var format = repository.FileFormat.GetByExtension(".dwg")!;
			var originalId = format.Id;

			format.Name = "Our drawing format";
			format.Description = "Customized by the user";

			var updated = repository.FileFormat.Update(format);

			Assert.True(updated.Validation.IsValid);

			var reopened = Open();
			var stored = reopened.FileFormat.GetById(originalId);

			Assert.NotNull(stored);
			Assert.Equal("Our drawing format", stored!.Name);
			Assert.Equal("Customized by the user", stored.Description);
			Assert.Equal(originalCount, reopened.FileFormat.GetAll().Count);
		}

		[Fact(DisplayName =	"Creating a file format normalizes its extension and persists its values")]
		public void Create_NormalizesAndPersistsExtension()
		{
			var repository = Open();

			var result = repository.FileFormat.Create(
				"Custom",
				"Custom document format",
				" XYZ ");

			Assert.True(result.Validation.IsValid);
			Assert.NotNull(result.Value);
			Assert.Equal(".xyz", result.Value!.Extension);

			var stored = Open().FileFormat.GetByExtension("XYZ");

			Assert.NotNull(stored);
			Assert.Equal(result.Value.Id, stored!.Id);
			Assert.Equal("Custom", stored.Name);
		}

		[Theory(DisplayName ="Equivalent file extensions cannot be registered more than once")]
		[InlineData(".dwg")]
		[InlineData("DWG")]
		[InlineData(" .DwG ")]
		public void Create_DuplicateExtension_IsRejected(string extension)
		{
			var repository = Open();
			var original = repository.FileFormat.GetByExtension(".dwg")!;
			var originalCount = repository.FileFormat.GetAll().Count;

			var result = repository.FileFormat.Create(
				"Duplicate",
				"Duplicate format",
				extension);

			Assert.Null(result.Value);
			Assert.False(result.Validation.IsValid);

			Assert.Equal(originalCount, repository.FileFormat.GetAll().Count);
			Assert.Equal(
				original.Id,
				repository.FileFormat.GetByExtension(".dwg")!.Id);
		}

		[Theory(DisplayName = "Invalid file extensions are rejected without inserting records")]
		[InlineData("")]
		[InlineData(".")]
		[InlineData("*.dwg")]
		[InlineData("two words")]
		[InlineData("abcdefghijklmnop")]
		public void Create_InvalidExtension_IsRejected(string extension)
		{
			var repository = Open();
			var originalCount = repository.FileFormat.GetAll().Count;

			var result = repository.FileFormat.Create(
				"Invalid",
				"",
				extension);

			Assert.Null(result.Value);
			Assert.False(result.Validation.IsValid);

			var error = Assert.Single(result.Validation.Errors);

			Assert.Equal("Extension", error.PropertyName);
			Assert.Equal(originalCount, repository.FileFormat.GetAll().Count);
		}

		[Fact(DisplayName =	"Updating a file format to an existing extension is rejected")]
		public void Update_DuplicateExtension_PreservesOriginal()
		{
			var repository = Open();
			var format = repository.FileFormat.GetByExtension(".pdf")!;
			var id = format.Id;

			format.Extension = "DWG";

			var result = repository.FileFormat.Update(format);

			Assert.Null(result.Value);
			Assert.False(result.Validation.IsValid);
			Assert.Equal(".pdf", repository.FileFormat.GetById(id)!.Extension);
		}

		[Fact(DisplayName = "An outdated file format cannot overwrite a newer saved version")]
		public void Update_OutdatedVersion_IsRejected()
		{
			var repository = Open();

			var first = repository.FileFormat.GetByExtension(".dwg")!;
			var second = repository.FileFormat.GetById(first.Id)!;

			first.Description = "First change";

			Assert.True(repository.FileFormat.Update(first).Validation.IsValid);

			second.Description = "Second change";

			var result = repository.FileFormat.Update(second);

			Assert.Null(result.Value);
			Assert.False(result.Validation.IsValid);

			Assert.Equal(
				"First change",
				repository.FileFormat.GetById(first.Id)!.Description);
		}

		[Fact(DisplayName =	"Editing a file format preserves existing document references")]
		public void Update_PreservesDocumentReference()
		{
			var pdsx = Open();

			var project = pdsx.Project.Create(
				"TEST-81346",
				_directory,
				"File format reference test");

			Assert.True(project.Validation.IsValid);

			var format = pdsx.FileFormat.GetByExtension(".dwg")!;

			var mdbx = _initializer.InitializeMdbx(
				Path.Combine(_directory, "Project.mdbx"));

			var document = mdbx.Document.Create(
				project.Value!.Id,
				format.Id,
				"Drawing01",
				Path.Combine(_directory, "Drawing01.dwg"));

			Assert.True(document.Validation.IsValid);

			format.Name = "Customized DWG";

			Assert.True(pdsx.FileFormat.Update(format).Validation.IsValid);

			var stored = mdbx.Document.GetById(document.Value!.Id)!;
			var resolved = pdsx.FileFormat.GetById(stored.FileFormatId);

			Assert.NotNull(resolved);
			Assert.Equal(format.Id, resolved!.Id);
			Assert.Equal("Customized DWG", resolved.Name);
		}


		[Fact(DisplayName = "A file format can select, replace and clear its default program association")]
		public void SetDefault_SelectReplaceAndClear_PersistsSelection()
		{
			var repository = Open();
			var format = repository.FileFormat.GetByExtension(".dwg")!;

			var firstProgram = AddProgram(repository, "FirstCAD");
			var secondProgram = AddProgram(repository, "SecondCAD");

			var first = repository.FileFormatProgram.Create(
				format.Id, firstProgram.Id, "\"{file}\"");

			var second = repository.FileFormatProgram.Create(
				format.Id, secondProgram.Id, "/open \"{file}\"");

			Assert.True(first.Validation.IsValid);
			Assert.True(second.Validation.IsValid);

			Assert.Null(repository.FileFormatProgram.GetDefault(format.Id));

			var selected = repository.FileFormatProgram.SetDefault(
				format,
				first.Value!.Id);

			Assert.True(selected.Validation.IsValid);
			Assert.NotNull(selected.Value);
			Assert.NotEqual(format.LoadedVersion, selected.Value!.LoadedVersion);

			Assert.Equal(
				first.Value.Id,
				Open().FileFormatProgram.GetDefault(format.Id)!.Id);

			var replaced = repository.FileFormatProgram.SetDefault(
				selected.Value,
				second.Value!.Id);

			Assert.True(replaced.Validation.IsValid);

			var reopened = Open();
			var defaultAssociation =
				reopened.FileFormatProgram.GetDefault(format.Id);

			Assert.NotNull(defaultAssociation);
			Assert.Equal(second.Value.Id, defaultAssociation!.Id);

			// Replacing the default must not delete the previous association.
			Assert.Equal(
				2,
				reopened.FileFormatProgram.GetByFileFormatId(format.Id).Count);

			var cleared = repository.FileFormatProgram.SetDefault(
				replaced.Value!,
				null);

			Assert.True(cleared.Validation.IsValid);
			Assert.Null(cleared.Value!.DefaultProgramAssociationId);
			Assert.Null(Open().FileFormatProgram.GetDefault(format.Id));
		}

		[Fact(DisplayName =	"A default association must belong to the selected file format")]
		public void SetDefault_AssociationFromAnotherFormat_IsRejected()
		{
			var repository = Open();
			var dwg = repository.FileFormat.GetByExtension(".dwg")!;
			var pdf = repository.FileFormat.GetByExtension(".pdf")!;
			var program = AddProgram(repository, "SomeProgram");

			var association = repository.FileFormatProgram.Create(
				pdf.Id, program.Id, "\"{file}\"");

			Assert.True(association.Validation.IsValid);

			var result = repository.FileFormatProgram.SetDefault(
				dwg,
				association.Value!.Id);

			Assert.Null(result.Value);
			Assert.False(result.Validation.IsValid);

			var error = Assert.Single(result.Validation.Errors);

			Assert.Equal(
				nameof(IFileFormat.DefaultProgramAssociationId),
				error.PropertyName);

			Assert.Null(repository.FileFormatProgram.GetDefault(dwg.Id));
		}

		[Fact(DisplayName =	"A missing association cannot be selected as the default")]
		public void SetDefault_MissingAssociation_IsRejected()
		{
			var repository = Open();
			var format = repository.FileFormat.GetByExtension(".dwg")!;

			var result = repository.FileFormatProgram.SetDefault(
				format,
				Guid.NewGuid());

			Assert.Null(result.Value);
			Assert.False(result.Validation.IsValid);
			Assert.Null(repository.FileFormatProgram.GetDefault(format.Id));
		}

		[Fact(DisplayName =	"An outdated format cannot overwrite a newer default selection")]
		public void SetDefault_OutdatedFormat_IsRejected()
		{
			var repository = Open();
			var firstFormat = repository.FileFormat.GetByExtension(".dwg")!;
			var outdatedFormat = repository.FileFormat.GetById(firstFormat.Id)!;

			var firstProgram = AddProgram(repository, "FirstCAD");
			var secondProgram = AddProgram(repository, "SecondCAD");

			var first = repository.FileFormatProgram.Create(
				firstFormat.Id, firstProgram.Id, "");

			var second = repository.FileFormatProgram.Create(
				firstFormat.Id, secondProgram.Id, "");

			Assert.True(first.Validation.IsValid);
			Assert.True(second.Validation.IsValid);

			var selected = repository.FileFormatProgram.SetDefault(
				firstFormat,
				first.Value!.Id);

			Assert.True(selected.Validation.IsValid);

			var rejected = repository.FileFormatProgram.SetDefault(
				outdatedFormat,
				second.Value!.Id);

			Assert.Null(rejected.Value);
			Assert.False(rejected.Validation.IsValid);

			Assert.Equal(
				first.Value.Id,
				repository.FileFormatProgram.GetDefault(firstFormat.Id)!.Id);
		}

		[Fact(DisplayName =	"Deleting the default association also clears the format's default selection")]
		public void Delete_DefaultAssociation_ClearsSelection()
		{
			var repository = Open();
			var format = repository.FileFormat.GetByExtension(".dwg")!;
			var program = AddProgram(repository, "SomeCAD");

			var created = repository.FileFormatProgram.Create(
				format.Id, program.Id, "");

			Assert.True(created.Validation.IsValid);

			var selected = repository.FileFormatProgram.SetDefault(
				format,
				created.Value!.Id);

			Assert.True(selected.Validation.IsValid);

			var deleted = repository.FileFormatProgram.Delete(created.Value);

			Assert.True(deleted.IsValid);

			var reopened = Open();

			Assert.Null(reopened.FileFormatProgram.GetById(created.Value.Id));
			Assert.Null(reopened.FileFormatProgram.GetDefault(format.Id));
			Assert.Null(
				reopened.FileFormat.GetById(format.Id)!.DefaultProgramAssociationId);

			Assert.NotNull(reopened.OpeningProgram.GetById(program.Id));
		}

		[Fact(DisplayName =	"Deleting another association preserves the selected default")]
		public void Delete_NonDefaultAssociation_PreservesSelection()
		{
			var repository = Open();
			var format = repository.FileFormat.GetByExtension(".dwg")!;

			var firstProgram = AddProgram(repository, "FirstCAD");
			var secondProgram = AddProgram(repository, "SecondCAD");

			var first = repository.FileFormatProgram.Create(
				format.Id, firstProgram.Id, "");

			var second = repository.FileFormatProgram.Create(
				format.Id, secondProgram.Id, "");

			Assert.True(first.Validation.IsValid);
			Assert.True(second.Validation.IsValid);

			Assert.True(repository.FileFormatProgram.SetDefault(
				format,
				first.Value!.Id).Validation.IsValid);

			Assert.True(
				repository.FileFormatProgram.Delete(second.Value!).IsValid);

			Assert.Equal(
				first.Value.Id,
				Open().FileFormatProgram.GetDefault(format.Id)!.Id);
		}

		[Fact(DisplayName =	"Editing file format metadata preserves its default program selection")]
		public void Update_FormatMetadata_PreservesDefault()
		{
			var repository = Open();
			var format = repository.FileFormat.GetByExtension(".dwg")!;
			var program = AddProgram(repository, "SomeCAD");

			var created = repository.FileFormatProgram.Create(
				format.Id, program.Id, "");

			Assert.True(created.Validation.IsValid);

			var selected = repository.FileFormatProgram.SetDefault(
				format,
				created.Value!.Id);

			Assert.True(selected.Validation.IsValid);

			var editable = selected.Value!;
			editable.Description = "Updated metadata";

			var updated = repository.FileFormat.Update(editable);

			Assert.True(updated.Validation.IsValid);
			Assert.Equal(
				created.Value.Id,
				updated.Value!.DefaultProgramAssociationId);
		}


		private static IOpeningProgram AddProgram(IPdsxRepository repository, string name)
		{
			var result = repository.OpeningProgram.Create(
				name,
				$@"C:\Programs\{name}.exe");

			Assert.True(result.Validation.IsValid);
			Assert.NotNull(result.Value);

			return result.Value!;
		}

		public void Dispose()
		{
			if (Directory.Exists(_directory))
				Directory.Delete(_directory, recursive: true);
		}
	}
}
