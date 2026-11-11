using ELPROCAD.DataAccess.Contracts.PDSX.Documents;
using ELPROCAD.DataAccess.Repositories.PDSX;
using Xunit;

namespace ELPROCAD.DataAccess.Tests.Database
{
	public sealed class FileFormatProgramTests : IDisposable
	{
		private readonly string _directory = Path.Combine(
			Path.GetTempPath(),
			"ELPROCAD.DataAccess.Tests",
			Guid.NewGuid().ToString("N"));

		private readonly IDataAccessInitializer _initializer =
			new DataAccessInitializer();

		private IPdsxRepository Open()
		{
			return _initializer.InitializePdsx(
				Path.Combine(_directory, "Project.pdsx"));
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

		[Fact(DisplayName =	"A format can have multiple programs with independently stored arguments")]
		public void Create_MultiplePrograms_PersistsSeparateAssociations()
		{
			var repository = Open();
			var dwg = repository.FileFormat.GetByExtension(".dwg")!;
			var pdf = repository.FileFormat.GetByExtension(".pdf")!;

			var firstProgram = AddProgram(repository, "FirstCAD");
			var secondProgram = AddProgram(repository, "SecondCAD");

			var first = repository.FileFormatProgram.Create(
				dwg.Id,
				firstProgram.Id,
				"\"{file}\"");

			var second = repository.FileFormatProgram.Create(
				dwg.Id,
				secondProgram.Id,
				"/open \"{file}\"");

			Assert.True(first.Validation.IsValid);
			Assert.True(second.Validation.IsValid);

			var reopened = Open();
			var associations = reopened.FileFormatProgram
				.GetByFileFormatId(dwg.Id);

			Assert.Equal(2, associations.Count);

			Assert.Contains(associations, item =>
				item.OpeningProgramId == firstProgram.Id &&
				item.Arguments == "\"{file}\"");

			Assert.Contains(associations, item =>
				item.OpeningProgramId == secondProgram.Id &&
				item.Arguments == "/open \"{file}\"");

			Assert.Empty(
				reopened.FileFormatProgram.GetByFileFormatId(pdf.Id));

			Assert.NotNull(
				reopened.FileFormatProgram.GetById(first.Value!.Id));
		}

		[Fact(DisplayName =	"The same format and program cannot be associated twice")]
		public void Create_DuplicateCombination_IsRejected()
		{
			var repository = Open();
			var format = repository.FileFormat.GetByExtension(".dwg")!;
			var program = AddProgram(repository, "SomeCAD");

			var first = repository.FileFormatProgram.Create(
				format.Id, program.Id, "\"{file}\"");

			Assert.True(first.Validation.IsValid);

			var duplicate = repository.FileFormatProgram.Create(
				format.Id, program.Id, "/different");

			Assert.Null(duplicate.Value);
			Assert.False(duplicate.Validation.IsValid);

			var stored = Assert.Single(
				repository.FileFormatProgram.GetByFileFormatId(format.Id));

			Assert.Equal("\"{file}\"", stored.Arguments);
		}

		[Theory(DisplayName = "Associations cannot reference a missing format or program")]
		[InlineData(true)]
		[InlineData(false)]
		public void Create_MissingReference_IsRejected(bool missingFormat)
		{
			var repository = Open();
			var format = repository.FileFormat.GetByExtension(".dwg")!;
			var program = AddProgram(repository, "SomeCAD");

			var result = repository.FileFormatProgram.Create(
				missingFormat ? Guid.NewGuid() : format.Id,
				missingFormat ? program.Id : Guid.NewGuid(),
				"");

			Assert.Null(result.Value);

			var error = Assert.Single(result.Validation.Errors);

			Assert.Equal(
				missingFormat
					? nameof(IFileFormatProgram.FileFormatId)
					: nameof(IFileFormatProgram.OpeningProgramId),
				error.PropertyName);
		}

		[Fact(DisplayName =	"Updating launch arguments preserves both association endpoints")]
		public void Update_Arguments_PreservesAssociation()
		{
			var repository = Open();
			var format = repository.FileFormat.GetByExtension(".dwg")!;
			var program = AddProgram(repository, "SomeCAD");

			var created = repository.FileFormatProgram.Create(
				format.Id, program.Id, "");

			Assert.True(created.Validation.IsValid);

			var association = created.Value!;
			association.Arguments = "/open \"{file}\"";

			var updated = repository.FileFormatProgram.Update(association);

			Assert.True(updated.Validation.IsValid);

			var stored = Open().FileFormatProgram.GetById(association.Id)!;

			Assert.Equal(format.Id, stored.FileFormatId);
			Assert.Equal(program.Id, stored.OpeningProgramId);
			Assert.Equal("/open \"{file}\"", stored.Arguments);
		}

		[Fact(DisplayName =	"Outdated associations cannot overwrite or delete newer changes")]
		public void UpdateAndDelete_OutdatedVersion_AreRejected()
		{
			var repository = Open();
			var format = repository.FileFormat.GetByExtension(".dwg")!;
			var program = AddProgram(repository, "SomeCAD");

			var created = repository.FileFormatProgram.Create(
				format.Id, program.Id, "");

			Assert.True(created.Validation.IsValid);

			var first = repository.FileFormatProgram.GetById(created.Value!.Id)!;
			var outdated = repository.FileFormatProgram.GetById(first.Id)!;

			first.Arguments = "/first";

			Assert.True(
				repository.FileFormatProgram.Update(first).Validation.IsValid);

			outdated.Arguments = "/second";

			Assert.False(
				repository.FileFormatProgram.Update(outdated).Validation.IsValid);

			Assert.False(
				repository.FileFormatProgram.Delete(outdated).IsValid);

			Assert.Equal(
				"/first",
				repository.FileFormatProgram.GetById(first.Id)!.Arguments);
		}

		[Fact(DisplayName =	"Deleting an association preserves its format and opening program")]
		public void Delete_RemovesOnlyAssociation()
		{
			var repository = Open();
			var format = repository.FileFormat.GetByExtension(".dwg")!;
			var program = AddProgram(repository, "SomeCAD");

			var created = repository.FileFormatProgram.Create(
				format.Id, program.Id, "");

			Assert.True(created.Validation.IsValid);

			var result = repository.FileFormatProgram.Delete(created.Value!);

			Assert.True(result.IsValid);
			Assert.Null(
				repository.FileFormatProgram.GetById(created.Value!.Id));

			Assert.NotNull(repository.FileFormat.GetById(format.Id));
			Assert.NotNull(repository.OpeningProgram.GetById(program.Id));
		}


		[Fact(DisplayName =	"Resolving a default returns its program and association-specific arguments")]
		public void ResolveDefault_ReturnsOpeningConfiguration()
		{
			var repository = Open();
			var format = repository.FileFormat.GetByExtension(".dwg")!;
			var program = AddProgram(repository, "SomeCAD");

			var created = repository.FileFormatProgram.Create(
				format.Id,
				program.Id,
				"/open \"{file}\"");

			Assert.True(created.Validation.IsValid);

			var selected = repository.FileFormatProgram.SetDefault(
				format,
				created.Value!.Id);

			Assert.True(selected.Validation.IsValid);

			var result = Open().FileFormatProgram.ResolveDefault(format.Id);

			Assert.True(result.Validation.IsValid);
			Assert.NotNull(result.Value);

			var configuration = result.Value!;

			Assert.Equal(format.Id, configuration.FileFormatId);
			Assert.Equal(created.Value.Id, configuration.AssociationId);
			Assert.Equal(program.Id, configuration.OpeningProgramId);
			Assert.Equal("SomeCAD", configuration.ProgramName);
			Assert.Equal(program.ExecutablePath, configuration.ExecutablePath);
			Assert.Equal("/open \"{file}\"", configuration.ArgumentsTemplate);
		}

		[Fact(DisplayName = "Resolving a format without a default returns a selection validation error")]
		public void ResolveDefault_NoSelection_ReturnsFailure()
		{
			var repository = Open();
			var format = repository.FileFormat.GetByExtension(".dwg")!;

			var result = repository.FileFormatProgram.ResolveDefault(format.Id);

			Assert.Null(result.Value);
			Assert.False(result.Validation.IsValid);

			var error = Assert.Single(result.Validation.Errors);

			Assert.Equal(
				nameof(IFileFormat.DefaultProgramAssociationId),
				error.PropertyName);
		}

		[Fact(DisplayName =	"Resolving an unknown file format returns an identifier validation error")]
		public void ResolveDefault_UnknownFormat_ReturnsFailure()
		{
			var result = Open().FileFormatProgram.ResolveDefault(Guid.NewGuid());

			Assert.Null(result.Value);
			Assert.False(result.Validation.IsValid);

			var error = Assert.Single(result.Validation.Errors);

			Assert.Equal(nameof(IFileFormatProgram.FileFormatId), error.PropertyName);
		}

		[Fact(DisplayName =	"Resolving a default uses the latest saved executable path and arguments")]
		public void ResolveDefault_UsesUpdatedConfiguration()
		{
			var repository = Open();
			var format = repository.FileFormat.GetByExtension(".dwg")!;
			var program = AddProgram(repository, "SomeCAD");

			var created = repository.FileFormatProgram.Create(
				format.Id,
				program.Id,
				"\"{file}\"");

			Assert.True(created.Validation.IsValid);

			Assert.True(repository.FileFormatProgram.SetDefault(
				format,
				created.Value!.Id).Validation.IsValid);

			var original = repository.FileFormatProgram.ResolveDefault(format.Id);

			Assert.True(original.Validation.IsValid);

			program.ExecutablePath = @"C:\Updated\SomeCAD.exe";

			Assert.True(
				repository.OpeningProgram.Update(program).Validation.IsValid);

			var association = created.Value;
			association.Arguments = "/updated \"{file}\"";

			Assert.True(
				repository.FileFormatProgram.Update(association).Validation.IsValid);

			var updated = Open().FileFormatProgram.ResolveDefault(format.Id);

			Assert.True(updated.Validation.IsValid);
			Assert.Equal(
				@"C:\Updated\SomeCAD.exe",
				updated.Value!.ExecutablePath);

			Assert.Equal(
				"/updated \"{file}\"",
				updated.Value.ArgumentsTemplate);

			// Previously returned snapshots remain unchanged.
			Assert.Equal(
				"\"{file}\"",
				original.Value!.ArgumentsTemplate);

			Assert.NotEqual(
				original.Value.ExecutablePath,
				updated.Value.ExecutablePath);
		}

		public void Dispose()
		{
			if (Directory.Exists(_directory))
				Directory.Delete(_directory, recursive: true);
		}
	}
}
