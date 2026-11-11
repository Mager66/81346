using DevExpress.Xpo;
using DevExpress.Xpo.DB.Exceptions;
using ELPROCAD.CAD.Data;
using ELPROCAD.CAD.Data.CRUD;
using ELPROCAD.CAD.Data.CRUD.Classification;
using ELPROCAD.CAD.Data.Model.Classification;
using ELPROCAD.CAD.Data.Provider;
using ELPROCAD.CAD.Data.Validation.VistDb;
using FluentValidation.Results;
using Microsoft.Extensions.DependencyInjection;
using System.Data.Common;
using Xunit;

namespace ELPROCAD.CAD.Data.Tests.Classification
{
	public sealed class MainClassificationTests : IDisposable
	{
		private readonly string _directory;
		private readonly ServiceProvider _provider;
		private readonly QueryMainClassification _query;		

		public MainClassificationTests()
		{
			_directory=Path.Combine(Path.GetTempPath(), "ELPROCAD.Tests", Guid.NewGuid().ToString("N"));

			Directory.CreateDirectory(_directory);

			var connectiosnString = new DbConnectionStringBuilder
			{
				["XpoProvider"] = "VistaDB6",
				["Data Source"] = Path.Combine(_directory, "Test.pdsx"),
				["Open Mode"] = "MultiProcessReadWrite",
				["Pooling"] = false
			};

			var services = new ServiceCollection();

			services.AddElprocadDataAccess(connectiosnString.ConnectionString);

			_provider = services.BuildServiceProvider();

			using (var scope = _provider.CreateScope())
			{
				var uow = scope.ServiceProvider.GetRequiredService<UnitOfWork>();

				uow.UpdateSchema(
					typeof(MainClass),
					typeof(SubClass));
			}

			_query = _provider.GetRequiredService<QueryMainClassification>();
		}

		[Fact]
		public void Create_ValidValues_PersistObjectAndVersion()
		{
			var created = CreateValid("AA");

			var loaded=_query.GetById(created.Id);

			Assert.NotNull(loaded);
			Assert.Equal("AA", loaded!.Abbrevation);
			Assert.Equal("Ursprunglig beskrivning", loaded.Tooltip);
			Assert.Equal("image.png", loaded.Image);

			Assert.True(created.LoadedVersion.HasValue);
			Assert.Equal(created.LoadedVersion, loaded.LoadedVersion);
		}

		[Fact]
		public void Create_DuplicateAbbrevation_ReturnsFieldError()
		{
			var original = CreateValid("AA");

			var result = _query.Create(
				"AA",
				"En annan beskrivning",
				"other.png");

			Assert.Null(result.Value);
			Assert.False(result.Validation.IsValid);

			Assert.Contains(
				result.Validation.Errors,
				error => error.PropertyName ==
					nameof(MainClass.Abbrevation));

			var stored = Assert.Single(_query.GetAll());

			Assert.Equal(original.Id, stored.Id);
			Assert.Equal(original.Tooltip, stored.Tooltip);
		}

		[Fact]
		public void Update_DuplicateAbbrevation_PreservesStoredValues()
		{
			CreateValid("AA");
			var original = CreateValid("BB");

			var result = _query.Update(
				original.Id,
				"AA",
				"Får inte sparas",
				"changed.png",
				original.LoadedVersion);

			Assert.Null(result.Value);
			Assert.False(result.Validation.IsValid);

			Assert.Contains(
				result.Validation.Errors,
				error => error.PropertyName ==
					nameof(MainClass.Abbrevation));

			var stored = _query.GetById(original.Id);

			Assert.NotNull(stored);
			Assert.Equal("BB", stored!.Abbrevation);
			Assert.Equal(original.Tooltip, stored.Tooltip);
			Assert.Equal(original.Image, stored.Image);
			Assert.Equal(original.LoadedVersion, stored.LoadedVersion);
		}

		[Fact]
		public void DirectInsert_DuplicateAbbrevation_IsBlockedByDatabase()
		{
			CreateValid("AA");

			// Gå förbi query-klassens förkontroll.
			// Här måste själva databasindexet stoppa dubbletten.
			using (var scope = _provider.CreateScope())
			{
				var uow = scope.ServiceProvider
					.GetRequiredService<UnitOfWork>();

				var duplicate = new MainClass
				{
					Abbrevation = "AA",
					Tooltip = "Direkt insättning",
					Image = ""
				};

				var exception = Assert.ThrowsAny<Exception>(() =>
				{
					uow.Save(duplicate);
					uow.CommitChanges();
				});

				Assert.True(
					VistaDbErrors.IsDuplicateKey(exception),
					exception.ToString());
			}

			Assert.Single(_query.GetAll());
		}

		[Fact]
		public void Update_StaleVersion_PreservesOtherUsersChanges()
		{
			var created = CreateValid("AA");

			// Två separata laddningar, som två öppna fönster.
			var first = _query.GetById(created.Id)!;
			var second = _query.GetById(created.Id)!;

			var firstResult = _query.Update(
				first.Id,
				first.Abbrevation,
				"Sparat av användare 1",
				first.Image,
				first.LoadedVersion);

			Assert.True(
				firstResult.Validation.IsValid,
				Describe(firstResult.Validation));

			var secondResult = _query.Update(
				second.Id,
				second.Abbrevation,
				"Sparat av användare 2",
				second.Image,
				second.LoadedVersion);

			Assert.Null(secondResult.Value);
			Assert.False(secondResult.Validation.IsValid);

			Assert.Contains(
				secondResult.Validation.Errors,
				error => error.PropertyName == string.Empty);

			var stored = _query.GetById(created.Id);

			Assert.Equal("Sparat av användare 1", stored!.Tooltip);
		}

		[Fact]
		public void Update_ReturnedVersion_AllowsNextUpdate()
		{
			var original = CreateValid("AA");

			var first = _query.Update(
				original.Id,
				original.Abbrevation,
				"Första ändringen",
				original.Image,
				original.LoadedVersion);

			Assert.True(first.Validation.IsValid, Describe(first.Validation));
			Assert.NotNull(first.Value);
			Assert.True(first.Value!.LoadedVersion.HasValue);
			Assert.NotEqual(
				original.LoadedVersion,
				first.Value.LoadedVersion);

			var second = _query.Update(
				first.Value.Id,
				first.Value.Abbrevation,
				"Andra ändringen",
				first.Value.Image,
				first.Value.LoadedVersion);

			Assert.True(
				second.Validation.IsValid,
				Describe(second.Validation));

			var stored = _query.GetById(original.Id);

			Assert.Equal("Andra ändringen", stored!.Tooltip);
		}

		[Fact]
		public void ConcurrentSessions_SecondCommitIsRejected()
		{
			var created = CreateValid("AA");

			using (var firstScope = _provider.CreateScope())
			using (var secondScope = _provider.CreateScope())
			{
				var first = firstScope.ServiceProvider
					.GetRequiredService<UnitOfWork>();

				var second = secondScope.ServiceProvider
					.GetRequiredService<UnitOfWork>();

				first.LockingOption = LockingOption.Optimistic;
				second.LockingOption = LockingOption.Optimistic;

				// Båda läser innan den första sparar.
				var firstObject =
					first.GetObjectByKey<MainClass>(created.Id);

				var secondObject =
					second.GetObjectByKey<MainClass>(created.Id);

				firstObject.Tooltip = "Första sessionen";
				first.Save(firstObject);
				first.CommitChanges();

				secondObject.Tooltip = "Andra sessionen";

				Assert.Throws<LockingException>(() =>
				{
					second.Save(secondObject);
					second.CommitChanges();
				});
			}

			var stored = _query.GetById(created.Id);

			Assert.Equal("Första sessionen", stored!.Tooltip);
		}

		[Fact]
		public void Create_DuplicateInsertedAfterCheck_ReturnsValidationError()
		{
			var database = _provider.GetRequiredService<DatabaseAccess>();
			var competingInsertCompleted = false;
			Guid? competingId = null;

			var query = new QueryMainClassification(
				database,
				beforePersist: () =>
				{
					// Den första operationens dubblettkontroll har passerat.
					// En separat anslutning hinner nu skapa förkortningen.
					var competing = _query.Create(
						"AA",
						"Sparad av den andra användaren",
						"other.png");

					Assert.True(
						competing.Validation.IsValid,
						Describe(competing.Validation));

					Assert.NotNull(competing.Value);

					competingId = competing.Value!.Id;
					competingInsertCompleted = true;
				});

			// Ska returnera ett valideringsfel, inte kasta databasundantaget.
			var result = query.Create(
				"AA",
				"Denna post får inte sparas",
				"first.png");

			Assert.True(competingInsertCompleted);
			Assert.Null(result.Value);
			Assert.False(result.Validation.IsValid);

			// Felmeddelandet från catch-blocket för databasens unika index.
			var error = Assert.Single(result.Validation.Errors);

			Assert.Equal(string.Empty, error.PropertyName);
			Assert.Equal(
				"Posten kunde inte sparas eftersom en unik nyckel redan finns. " +
				"Förkortningen kan ha registrerats av någon annan. Läs in senaste data.",
				error.ErrorMessage);

			// Bara den konkurrerande insättningen ska finnas kvar.
			var stored = Assert.Single(_query.GetAll());

			Assert.Equal(competingId.GetValueOrDefault(), stored.Id);
			Assert.Equal("AA", stored.Abbrevation);
			Assert.Equal("Sparad av den andra användaren", stored.Tooltip);
			Assert.Equal("other.png", stored.Image);
		}

		[Fact]
		public void GenericNew_ValidEntity_CopiesAndPersistsValues()
		{
			var writer = CreateWriter();
			var source = NewMainClass();

			var databaseValidated = false;
			var valuesCopied = false;

			var result = writer.New<MainClass>(
				source,
				copyValues: (uow, from, target) =>
				{
					Assert.True(databaseValidated);
					Assert.Same(source, from);
					Assert.NotSame(source, target);
					Assert.Equal(source.Id, target.Id);

					target.Abbrevation = from.Abbrevation;
					target.Tooltip = from.Tooltip;
					target.Image = from.Image;

					valuesCopied = true;
				},
				validateDatabase: (uow, entity) =>
				{
					Assert.Same(source, entity);
					databaseValidated = true;

					return new ValidationResult();
				});

			Assert.True(result.Validation.IsValid, Describe(result.Validation));
			Assert.True(valuesCopied);
			Assert.NotNull(result.Value);
			Assert.NotSame(source, result.Value);
			Assert.True(result.Value!.LoadedVersion.HasValue);

			using var scope = _provider.CreateScope();
			var uow = scope.ServiceProvider.GetRequiredService<UnitOfWork>();
			var saved = uow.GetObjectByKey<MainClass>(source.Id);

			Assert.NotNull(saved);
			Assert.Equal(source.Abbrevation, saved.Abbrevation);
			Assert.Equal(source.Tooltip, saved.Tooltip);
			Assert.Equal(source.Image, saved.Image);
		}

		[Fact]
		public void GenericNew_InvalidEntity_DoesNotInvokeCallbacks()
		{
			var writer = CreateWriter();
			var source = NewMainClass("");

			var result = writer.New<MainClass>(
				source,
				copyValues: (uow, from, target) =>
					throw new InvalidOperationException(
						"Kopieringen ska inte köras."),
				validateDatabase: (uow, entity) =>
					throw new InvalidOperationException(
						"Databasvalideringen ska inte köras."));

			Assert.Null(result.Value);
			Assert.False(result.Validation.IsValid);

			Assert.Contains(
				result.Validation.Errors,
				error => error.PropertyName == nameof(MainClass.Abbrevation));

			using var scope = _provider.CreateScope();
			var uow = scope.ServiceProvider.GetRequiredService<UnitOfWork>();

			Assert.Empty(new XPQuery<MainClass>(uow).ToList());
		}

		[Fact]
		public void GenericNew_DatabaseValidationFails_DoesNotPersist()
		{
			var writer = CreateWriter();
			var source = NewMainClass();

			var expected = new ValidationResult(new[]
			{
		new ValidationFailure(
			nameof(MainClass.Abbrevation),
			"Test: förkortningen är upptagen.")
	});

			var result = writer.New<MainClass>(
				source,
				copyValues: (uow, from, target) =>
					throw new InvalidOperationException(
						"Kopieringen ska inte köras."),
				validateDatabase: (uow, entity) => expected);

			Assert.Null(result.Value);
			Assert.Same(expected, result.Validation);

			using var scope = _provider.CreateScope();
			var uow = scope.ServiceProvider.GetRequiredService<UnitOfWork>();

			Assert.Null(uow.GetObjectByKey<MainClass>(source.Id));
		}

		[Fact]
		public void MainClassNew_ValidEntity_PersistsAllFields()
		{
			var writer = CreateWriter();
			var source = NewMainClass();

			// Anropar överlagringen från partial-filen.
			var result = writer.New(source);

			Assert.True(result.Validation.IsValid, Describe(result.Validation));
			Assert.NotNull(result.Value);
			Assert.Equal(source.Id, result.Value!.Id);
			Assert.True(result.Value.LoadedVersion.HasValue);

			using var scope = _provider.CreateScope();
			var uow = scope.ServiceProvider.GetRequiredService<UnitOfWork>();
			var saved = uow.GetObjectByKey<MainClass>(source.Id);

			Assert.NotNull(saved);
			Assert.Equal(source.Abbrevation, saved.Abbrevation);
			Assert.Equal(source.Tooltip, saved.Tooltip);
			Assert.Equal(source.Image, saved.Image);
		}

		[Fact]
		public void MainClassNew_Duplicate_ReturnsFieldError()
		{
			var writer = CreateWriter();

			var first = NewMainClass();
			var firstResult = writer.New(first);

			Assert.True(
				firstResult.Validation.IsValid,
				Describe(firstResult.Validation));

			var second = NewMainClass();
			second.Tooltip = "Får inte ersätta originalet";

			var result = writer.New(second);

			Assert.Null(result.Value);
			Assert.False(result.Validation.IsValid);

			Assert.Contains(
				result.Validation.Errors,
				error => error.PropertyName == nameof(MainClass.Abbrevation));

			using var scope = _provider.CreateScope();
			var uow = scope.ServiceProvider.GetRequiredService<UnitOfWork>();
			var saved = Assert.Single(new XPQuery<MainClass>(uow).ToList());

			Assert.Equal(first.Id, saved.Id);
			Assert.Equal(first.Tooltip, saved.Tooltip);
		}
		[Fact]
		public void MainClassNew_DuplicateAfterCheck_ReturnsDatabaseValidationError()
		{
			var competingWriter = CreateWriter();
			var competing = NewMainClass();
			competing.Tooltip = "Sparad av den andra användaren";

			var callbackExecuted = false;

			var writer = CreateWriter(beforePersist: () =>
			{
				var competingResult = competingWriter.New(competing);

				Assert.True(
					competingResult.Validation.IsValid,
					Describe(competingResult.Validation));

				callbackExecuted = true;
			});

			var result = writer.New(NewMainClass());

			Assert.True(callbackExecuted);
			Assert.Null(result.Value);
			Assert.False(result.Validation.IsValid);

			var error = Assert.Single(result.Validation.Errors);

			Assert.Equal(string.Empty, error.PropertyName);
			Assert.Equal(
				"Posten kunde inte sparas eftersom en unik nyckel redan finns. " +
				"Förkortningen kan ha registrerats av någon annan. Läs in senaste data.",
				error.ErrorMessage);

			using var scope = _provider.CreateScope();
			var uow = scope.ServiceProvider.GetRequiredService<UnitOfWork>();
			var saved = Assert.Single(new XPQuery<MainClass>(uow).ToList());

			Assert.Equal(competing.Id, saved.Id);
			Assert.Equal(competing.Tooltip, saved.Tooltip);
		}

		private MainClass CreateValid(string abbrevation)
		{
			var result = _query.Create(
				abbrevation,
				"Ursprunglig beskrivning",
				"image.png");

			Assert.True(
				result.Validation.IsValid,
				Describe(result.Validation));

			Assert.NotNull(result.Value);

			return result.Value!;
		}

		private static string Describe(ValidationResult validation)
		{
			return string.Join(
				Environment.NewLine,
				validation.Errors.Select(x => x.ErrorMessage));
		}

		private Create CreateWriter(Action? beforePersist = null)
		{
			var database = _provider.GetRequiredService<DatabaseAccess>();

			return beforePersist == null
				? new Create(database)
				: new Create(database, beforePersist);
		}

		private static MainClass NewMainClass(string abbrevation = "AA")
		{
			return new MainClass
			{
				Abbrevation = abbrevation,
				Tooltip = "Beskrivning",
				Image = "image.png"
			};
		}

		public void Dispose()
		{
			_provider.Dispose();

			if (Directory.Exists(_directory))
				Directory.Delete(_directory, recursive: true);
		}
	}
}
