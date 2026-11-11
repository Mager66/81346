using ELPROCAD.DataAccess.Repositories.PDSX;
using Xunit;

namespace ELPROCAD.DataAccess.Tests.Database
{
	public sealed class OpeningProgramTests : IDisposable
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

		[Fact(DisplayName =	"Creating a program persists its configuration without requiring an installed executable")]
		public void Create_ValidConfiguration_IsPersisted()
		{
			var repository = Open();
			var executablePath = Path.Combine(
				_directory,
				"NotInstalled",
				"SomeCAD.exe");

			Assert.False(File.Exists(executablePath));

			var result = repository.OpeningProgram.Create(
				"SomeCAD",
				executablePath);

			Assert.True(result.Validation.IsValid);
			Assert.NotNull(result.Value);
			Assert.NotNull(result.Value!.LoadedVersion);

			var stored = Open().OpeningProgram.GetById(result.Value.Id);

			Assert.NotNull(stored);
			Assert.Equal("SomeCAD", stored!.Name);
			Assert.Equal(executablePath, stored.ExecutablePath);
		}

		[Theory(DisplayName = "Invalid program configuration is rejected without inserting data")]
		[InlineData("Name", 0)]
		[InlineData("Name", 51)]
		[InlineData("ExecutablePath", 0)]
		[InlineData("ExecutablePath", 1025)]
		public void Create_InvalidConfiguration_IsRejected(string propertyName, int length)
		{
			var repository = Open();
			var invalidValue = new string('A', length);

			var result = repository.OpeningProgram.Create(
				propertyName == "Name" ? invalidValue : "SomeCAD",
				propertyName == "ExecutablePath"
					? invalidValue
					: @"C:\Programs\SomeCAD.exe");

			Assert.Null(result.Value);
			Assert.False(result.Validation.IsValid);

			var error = Assert.Single(result.Validation.Errors);

			Assert.Equal(propertyName, error.PropertyName);
			Assert.Empty(repository.OpeningProgram.GetAll());
		}

		[Fact(DisplayName =	"Updating a program preserves its identity and persists the new executable path")]
		public void Update_ValidConfiguration_PreservesIdentity()
		{
			var repository = Open();

			var created = repository.OpeningProgram.Create(
				"SomeCAD",
				@"C:\Old\SomeCAD.exe");

			Assert.True(created.Validation.IsValid);

			var program = created.Value!;
			var id = program.Id;
			var previousVersion = program.LoadedVersion;

			program.Name = "SomeCAD updated";
			program.ExecutablePath = @"C:\New\SomeCAD.exe";

			var updated = repository.OpeningProgram.Update(program);

			Assert.True(updated.Validation.IsValid);
			Assert.NotNull(updated.Value);
			Assert.Equal(id, updated.Value!.Id);
			Assert.NotEqual(previousVersion, updated.Value.LoadedVersion);

			var stored = Open().OpeningProgram.GetById(id);

			Assert.NotNull(stored);
			Assert.Equal("SomeCAD updated", stored!.Name);
			Assert.Equal(@"C:\New\SomeCAD.exe", stored.ExecutablePath);
		}

		[Fact(DisplayName =	"Invalid program updates leave stored configuration unchanged")]
		public void Update_InvalidConfiguration_PreservesStoredData()
		{
			var repository = Open();

			var created = repository.OpeningProgram.Create(
				"SomeCAD",
				@"C:\Programs\SomeCAD.exe");

			Assert.True(created.Validation.IsValid);

			var program = created.Value!;
			program.ExecutablePath = "";

			var result = repository.OpeningProgram.Update(program);

			Assert.Null(result.Value);
			Assert.False(result.Validation.IsValid);

			Assert.Equal(
				@"C:\Programs\SomeCAD.exe",
				Open().OpeningProgram.GetById(program.Id)!.ExecutablePath);
		}

		[Fact(DisplayName =	"An outdated program configuration cannot overwrite a newer version")]
		public void Update_OutdatedVersion_IsRejected()
		{
			var repository = Open();

			var created = repository.OpeningProgram.Create(
				"SomeCAD",
				@"C:\Original\SomeCAD.exe");

			Assert.True(created.Validation.IsValid);

			var first = repository.OpeningProgram.GetById(created.Value!.Id)!;
			var second = repository.OpeningProgram.GetById(first.Id)!;

			first.ExecutablePath = @"C:\First\SomeCAD.exe";

			Assert.True(
				repository.OpeningProgram.Update(first).Validation.IsValid);

			second.ExecutablePath = @"C:\Second\SomeCAD.exe";

			var result = repository.OpeningProgram.Update(second);

			Assert.Null(result.Value);
			Assert.False(result.Validation.IsValid);

			Assert.Equal(
				@"C:\First\SomeCAD.exe",
				Open().OpeningProgram.GetById(first.Id)!.ExecutablePath);
		}

		[Fact(DisplayName =	"Programs are returned in name order and unknown identifiers return null")]
		public void Read_ReturnsOrderedPrograms()
		{
			var repository = Open();

			Assert.True(repository.OpeningProgram.Create(
				"Zeta",
				@"C:\Programs\Zeta.exe").Validation.IsValid);

			Assert.True(repository.OpeningProgram.Create(
				"Alpha",
				@"C:\Programs\Alpha.exe").Validation.IsValid);

			Assert.Equal(
				new[] { "Alpha", "Zeta" },
				repository.OpeningProgram.GetAll()
					.Select(program => program.Name));

			Assert.Null(
				repository.OpeningProgram.GetById(Guid.NewGuid()));
		}

		public void Dispose()
		{
			if (Directory.Exists(_directory))
				Directory.Delete(_directory, recursive: true);
		}
	}
}
