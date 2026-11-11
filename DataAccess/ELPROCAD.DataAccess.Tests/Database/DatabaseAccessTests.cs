using ELPROCAD.DataAccess.Contracts.PDSX.ProjectData;
using ELPROCAD.DataAccess.CRUD;
using ELPROCAD.DataAccess.Database;
using ELPROCAD.DataAccess.Models.PDSX.Classification;
using ELPROCAD.DataAccess.Models.PDSX.ProjectData;
using ELPROCAD.DataAccess.Repositories.PDSX;
using ELPROCAD.DataAccess.Repositories.PDSX.Classification;
using ELPROCAD.DataAccess.Repositories.PDSX.Documents;
using ELPROCAD.DataAccess.Repositories.PDSX.ProjectData;
using ELPROCAD.DataAccess.Tests.Models;
using Microsoft.Extensions.DependencyInjection;
using System.Data.Common;
using Xunit;

namespace ELPROCAD.DataAccess.Tests.Database
{
	public sealed class DatabaseAccessTests : IDisposable
	{
		private readonly string _directory;
		private readonly IDatabaseAccess _database;

		public DatabaseAccessTests()
		{
			_directory = Path.Combine(
				Path.GetTempPath(),
				"ELPROCAD.DataAccess.Tests",
				Guid.NewGuid().ToString("N"));

			var databasePath = Path.Combine(
				_directory,
				"Test.pdsx");

			new DataAccessInitializer().Initialize(
				databasePath,
				typeof(InitializationTestEntity),
				typeof(MainClass));

			var connectionString = new DbConnectionStringBuilder
			{
				["XpoProvider"] = "VistaDB6",
				["Data Source"] = databasePath,
				["Open Mode"] = "MultiProcessReadWrite",
				["Pooling"] = false
			};

			_database = new DatabaseAccess(
				connectionString.ConnectionString);
		}

		[Fact(DisplayName = "A commited entity can be read by another operation")]
		public void Execute_CommittedEntity_CanBeReadByAnotherOperation()
		{
			var id = Guid.NewGuid();

			_database.Execute(unitOfWork =>
			{
				unitOfWork.Save(new InitializationTestEntity
				{
					Id = id,
					Name = "Saved entity"
				});

				unitOfWork.CommitChanges();
			});

			var loaded = _database.Execute(unitOfWork =>
				unitOfWork.GetObjectByKey<InitializationTestEntity>(id));

			Assert.NotNull(loaded);
			Assert.Equal("Saved entity", loaded!.Name);
		}

		[Fact(DisplayName = "Does not persist entity without commit")]
		public void Execute_WithoutCommit_DoesNotPersistEntity()
		{
			var id = Guid.NewGuid();

			_database.Execute(unitOfWork =>
			{
				unitOfWork.Save(new InitializationTestEntity
				{
					Id = id,
					Name = "Uncommitted entity"
				});
			});

			var loaded = _database.Execute(unitOfWork =>
				unitOfWork.GetObjectByKey<InitializationTestEntity>(id));

			Assert.Null(loaded);
		}

		[Fact(DisplayName = "Failed operation does not persist changes")]
		public void Execute_FailedOperation_DoesNotPersistPendingChanges()
		{
			var id = Guid.NewGuid();
			var expected = new InvalidOperationException(
				"Simulated operation failure.");

			var actual = Assert.Throws<InvalidOperationException>(() =>
				_database.Execute(unitOfWork =>
				{
					unitOfWork.Save(new InitializationTestEntity
					{
						Id = id,
						Name = "Failed entity"
					});

					throw expected;
				}));

			Assert.Same(expected, actual);

			// A subsequent operation must still be able to access the database.
			var loaded = _database.Execute(unitOfWork =>
				unitOfWork.GetObjectByKey<InitializationTestEntity>(id));

			Assert.Null(loaded);
		}

		[Fact(DisplayName =	"Reading an existing entity returns its stored values")]
		public void GetById_ExistingEntity_ReturnsStoredValues()
		{
			var id = Guid.NewGuid();
			var reader = new Read(_database);

			_database.Execute(unitOfWork =>
			{
				unitOfWork.Save(new InitializationTestEntity
				{
					Id = id,
					Name = "Existing entity"
				});

				unitOfWork.CommitChanges();
			});

			var entity = reader.GetById<InitializationTestEntity>(id);

			Assert.NotNull(entity);
			Assert.Equal(id, entity!.Id);
			Assert.Equal("Existing entity", entity.Name);
		}

		[Fact(DisplayName =	"Reading an unknown identifier returns null")]
		public void GetById_UnknownIdentifier_ReturnsNull()
		{
			var reader = new Read(_database);

			var entity = reader.GetById<InitializationTestEntity>(
				Guid.NewGuid());

			Assert.Null(entity);
		}

		[Fact(DisplayName =	"Reading all entities returns data usable after the session is closed")]
		public void GetAll_ReturnsMaterializedEntities()
		{
			var reader = new Read(_database);

			_database.Execute(unitOfWork =>
			{
				unitOfWork.Save(new InitializationTestEntity
				{
					Id = Guid.NewGuid(),
					Name = "First"
				});

				unitOfWork.Save(new InitializationTestEntity
				{
					Id = Guid.NewGuid(),
					Name = "Second"
				});

				unitOfWork.CommitChanges();
			});

			var entities = reader.GetAll<InitializationTestEntity>();

			Assert.Equal(2, entities.Count);
			Assert.Equal(
				new[] { "First", "Second" },
				entities.Select(entity => entity.Name).OrderBy(name => name));

			// Editing a returned POCO must not update the database implicitly.
			var edited = entities[0];
			edited.Name = "Local change";

			var stored = reader.GetById<InitializationTestEntity>(edited.Id);

			Assert.NotNull(stored);
			Assert.NotEqual(edited.Name, stored!.Name);
		}

		[Fact(DisplayName =	"Reading an empty table returns an empty collection")]
		public void GetAll_EmptyTable_ReturnsEmptyCollection()
		{
			var reader = new Read(_database);

			var entities = reader.GetAll<InitializationTestEntity>();

			Assert.Empty(entities);
		}


		[Fact(DisplayName = "PDSX returns main classifications ordered by abbreviation")]
		public void Pdsx_MainClass_GetAll_ReturnsOrderedClassifications()
		{
			_database.Execute(unitOfWork =>
			{
				unitOfWork.Save(new MainClass
				{
					Abbrevation = "MC",
					Tooltip = "Main component",
					Image = "main-component"
				});

				unitOfWork.Save(new MainClass
				{
					Abbrevation = "CE",
					Tooltip = "Common electric",
					Image = "common-electric"
				});

				unitOfWork.CommitChanges();
			});

			var read = new Read(_database);
			var create = new Create(_database);
			var update = new Update(_database);
			var delete = new Delete(_database);

			IPdsxRepository pdsx = new PdsxRepository(
				new MainClassRepository(read),
				new SubClassRepository(read),
				new ProjectRepository(read, create, update),
				new FileFormatRepository(read, create, update),
				new OpeningProgramRepository(read, create, update),
				new FileFormatProgramRepository(read, create, update, delete));

			var classes = pdsx.MainClass.GetAll();

			Assert.Equal(
				new[] { "CE", "MC" },
				classes.Select(entity => entity.Abbrevation));
		}

		[Fact(DisplayName =	"PDSX finds a main classification with all stored values")]
		public void Pdsx_MainClass_GetById_ReturnsStoredClassification()
		{
			var id = Guid.NewGuid();

			_database.Execute(unitOfWork =>
			{
				unitOfWork.Save(new MainClass
				{
					Id = id,
					Abbrevation = "MC",
					Tooltip = "Main component",
					Image = "main-component"
				});

				unitOfWork.CommitChanges();
			});

			var read = new Read(_database);
			var create = new Create(_database);
			var update = new Update(_database);
			var delete = new Delete(_database);

			IPdsxRepository pdsx = new PdsxRepository(
				new MainClassRepository(read),
				new SubClassRepository(read),
				new ProjectRepository(read, create, update),
				new FileFormatRepository(read, create, update),
				new OpeningProgramRepository(read, create, update),
				new FileFormatProgramRepository(read, create, update, delete));

			var mainClass = pdsx.MainClass.GetById(id);

			Assert.NotNull(mainClass);
			Assert.Equal(id, mainClass!.Id);
			Assert.Equal("MC", mainClass.Abbrevation);
			Assert.Equal("Main component", mainClass.Tooltip);
			Assert.Equal("main-component", mainClass.Image);
		}

		[Fact(DisplayName =	"PDSX returns null when the main classification does not exist")]
		public void Pdsx_MainClass_GetById_UnknownIdentifier_ReturnsNull()
		{
			var read = new Read(_database);
			var create = new Create(_database);
			var update = new Update(_database);
			var delete = new Delete(_database);

			IPdsxRepository pdsx = new PdsxRepository(
				new MainClassRepository(read),
				new SubClassRepository(read),
				new ProjectRepository(read, create, update),
				new FileFormatRepository(read, create, update),
				new OpeningProgramRepository(read, create, update),
				new FileFormatProgramRepository(read, create, update, delete));

			var mainClass = pdsx.MainClass.GetById(Guid.NewGuid());

			Assert.Null(mainClass);
		}

		[Fact(DisplayName =	"Initialization through dependency injection returns a usable PDSX repository")]
		public void InitializePdsx_ThroughDependencyInjection_ReturnsUsableRepository()
		{
			var services = new ServiceCollection();
			services.AddElprocadDataAccess();

			using var provider = services.BuildServiceProvider();

			var initializer = provider
				.GetRequiredService<IDataAccessInitializer>();

			var databasePath = Path.Combine(
				_directory,
				"Configuration.pdsx");

			var pdsx = initializer.InitializePdsx(databasePath);

			Assert.True(File.Exists(databasePath));
			Assert.Empty(pdsx.MainClass.GetAll());
		}

		[Fact(DisplayName =	"Reinitializing PDSX preserves existing classifications")]
		public void InitializePdsx_ExistingDatabase_PreservesClassifications()
		{
			var databasePath = Path.Combine(
				_directory,
				"Configuration.pdsx");

			IDataAccessInitializer initializer = new DataAccessInitializer();
			initializer.InitializePdsx(databasePath);

			var database = new DatabaseAccess(
				DatabaseConnectionStrings.Create(databasePath));

			var id = Guid.NewGuid();

			// Configuration writes are performed internally for test setup.
			database.Execute(unitOfWork =>
			{
				unitOfWork.Save(new MainClass
				{
					Id = id,
					Abbrevation = "MC",
					Tooltip = "Main component"
				});

				unitOfWork.CommitChanges();
			});

			var pdsx = initializer.InitializePdsx(databasePath);
			var mainClass = pdsx.MainClass.GetById(id);

			Assert.NotNull(mainClass);
			Assert.Equal("MC", mainClass!.Abbrevation);
		}

		[Fact(DisplayName =	"PDSX repositories remain bound to their own project databases")]
		public void InitializePdsx_TwoDatabases_KeepsRepositoriesIsolated()
		{
			IDataAccessInitializer initializer = new DataAccessInitializer();

			var firstPath = Path.Combine(_directory, "First.pdsx");
			var secondPath = Path.Combine(_directory, "Second.pdsx");

			var first = initializer.InitializePdsx(firstPath);
			var second = initializer.InitializePdsx(secondPath);

			var database = new DatabaseAccess(
				DatabaseConnectionStrings.Create(firstPath));

			database.Execute(unitOfWork =>
			{
				unitOfWork.Save(new MainClass
				{
					Abbrevation = "MC",
					Tooltip = "First project only"
				});

				unitOfWork.CommitChanges();
			});

			Assert.Single(first.MainClass.GetAll());
			Assert.Empty(second.MainClass.GetAll());
		}

		[Fact(DisplayName =	"Sub-classifications are filtered by parent and ordered by abbreviation")]
		public void SubClass_GetByMainClassId_FiltersAndOrders()
		{
			var (repository, database) = CreateClassificationDatabase();
			var firstId = Guid.NewGuid();

			database.Execute(unitOfWork =>
			{
				var first = new MainClass
				{
					Id = firstId,
					Abbrevation = "MC",
					Tooltip = "Main component"
				};

				var second = new MainClass
				{
					Abbrevation = "CE",
					Tooltip = "Common electric"
				};

				unitOfWork.Save(first);
				unitOfWork.Save(second);

				unitOfWork.Save(new SubClass
				{
					Abbrevation = "ZZ",
					MainClass = first
				});

				unitOfWork.Save(new SubClass
				{
					Abbrevation = "AA",
					MainClass = first
				});

				unitOfWork.Save(new SubClass
				{
					Abbrevation = "BB",
					MainClass = second
				});

				unitOfWork.CommitChanges();
			});

			var items = repository.SubClass.GetByMainClassId(firstId);

			Assert.Equal(
				new[] { "AA", "ZZ" },
				items.Select(item => item.Abbrevation));

			// Parent data must remain readable after the database operation ends.
			Assert.All(items, item =>
			{
				Assert.Equal(firstId, item.MainClass.Id);
				Assert.Equal("MC", item.MainClass.Abbrevation);
				Assert.Equal("Main component", item.MainClass.Tooltip);
			});

			var loaded = repository.SubClass.GetById(items[0].Id);

			Assert.NotNull(loaded);
			Assert.Equal("AA", loaded!.Abbrevation);
			Assert.Equal(firstId, loaded.MainClass.Id);
		}

		[Fact(DisplayName =	"An unknown main classification returns no sub-classifications")]
		public void SubClass_UnknownParent_ReturnsEmptyCollection()
		{
			var (repository, _) = CreateClassificationDatabase();

			Assert.Empty(
				repository.SubClass.GetByMainClassId(Guid.NewGuid()));
		}

		[Fact(DisplayName =	"The same sub-classification abbreviation is allowed under different parents")]
		public void SubClass_SameAbbreviation_DifferentParents_IsAllowed()
		{
			var (repository, database) = CreateClassificationDatabase();
			var firstId = Guid.NewGuid();
			var secondId = Guid.NewGuid();

			database.Execute(unitOfWork =>
			{
				var first = new MainClass
				{
					Id = firstId,
					Abbrevation = "MC"
				};

				var second = new MainClass
				{
					Id = secondId,
					Abbrevation = "CE"
				};

				unitOfWork.Save(first);
				unitOfWork.Save(second);

				unitOfWork.Save(new SubClass
				{
					Abbrevation = "AA",
					MainClass = first
				});

				unitOfWork.Save(new SubClass
				{
					Abbrevation = "AA",
					MainClass = second
				});

				unitOfWork.CommitChanges();
			});

			Assert.Single(repository.SubClass.GetByMainClassId(firstId));
			Assert.Single(repository.SubClass.GetByMainClassId(secondId));
		}

		[Fact(DisplayName =	"Duplicate sub-classification abbreviations under the same parent are rejected")]
		public void SubClass_SameAbbreviation_SameParent_IsRejected()
		{
			var (repository, database) = CreateClassificationDatabase();
			var parentId = Guid.NewGuid();

			database.Execute(unitOfWork =>
			{
				var parent = new MainClass
				{
					Id = parentId,
					Abbrevation = "MC"
				};

				unitOfWork.Save(parent);

				unitOfWork.Save(new SubClass
				{
					Abbrevation = "AA",
					Tooltip = "Original",
					MainClass = parent
				});

				unitOfWork.CommitChanges();
			});

			// Resolve the parent in the session used for the attempted insert.
			var exception = Record.Exception(() =>
				database.Execute(unitOfWork =>
				{
					var parent = unitOfWork.GetObjectByKey<MainClass>(parentId);

					Assert.NotNull(parent);

					unitOfWork.Save(new SubClass
					{
						Abbrevation = "AA",
						Tooltip = "Duplicate",
						MainClass = parent!
					});

					unitOfWork.CommitChanges();
				}));

			Assert.NotNull(exception);

			Assert.True(VistaDbErrors.IsDuplicateKey(exception), $"Expected a VistaDB duplicate-key error, but received: {exception}");

			var stored = Assert.Single(
				repository.SubClass.GetByMainClassId(parentId));

			Assert.Equal("Original", stored.Tooltip);

			var wrapped = new InvalidOperationException("The database operation failed.", exception);

			Assert.True(VistaDbErrors.IsDuplicateKey(wrapped));

			var aggregate = new AggregateException(
				new InvalidOperationException("Unrelated failure."),
				wrapped);

			Assert.True(VistaDbErrors.IsDuplicateKey(aggregate));
		}

		[Fact(DisplayName = "Creating a project through PDSX persists its identity and metadata")]
		public void Project_Create_ValidInput_IsPersisted()
		{
			var databasePath = Path.Combine(_directory, "CreateProject.pdsx");

			IDataAccessInitializer initializer = new DataAccessInitializer();
			var repository = initializer.InitializePdsx(databasePath);

			var result = repository.Project.Create(
				"TEST-81346",
				_directory,
				"Created through the repository");

			Assert.True(result.Validation.IsValid);
			Assert.NotNull(result.Value);
			Assert.NotEqual(Guid.Empty, result.Value!.Id);

			// Reopen through the public entry point to verify persisted data.
			var reopened = initializer.InitializePdsx(databasePath);
			var stored = reopened.Project.GetById(result.Value.Id);

			Assert.NotNull(stored);
			Assert.Equal("TEST-81346", stored!.Name);
			Assert.Equal(_directory, stored.Path);
			Assert.Equal("Created through the repository", stored.Description);

			Assert.Single(reopened.Project.GetAll());
		}

		[Theory(DisplayName = "Creating a project with missing required values returns field errors without inserting data")]
		[InlineData("", "Valid path", "Name")]
		[InlineData("   ", "Valid path", "Name")]
		[InlineData("Valid name", "", "Path")]
		[InlineData("Valid name", "   ", "Path")]
		public void Project_Create_MissingRequiredValue_IsRejected(string name, string path, string propertyName)
		{
			var databasePath = Path.Combine(_directory, "InvalidProject.pdsx");

			IDataAccessInitializer initializer = new DataAccessInitializer();
			var repository = initializer.InitializePdsx(databasePath);

			var result = repository.Project.Create(name, path, "");

			Assert.Null(result.Value);
			Assert.False(result.Validation.IsValid);

			var error = Assert.Single(result.Validation.Errors);

			Assert.Equal(propertyName, error.PropertyName);
			Assert.Empty(repository.Project.GetAll());
		}

		[Theory(DisplayName = "Creating a project rejects values exceeding the database column lengths")]
		[InlineData("Name", 51)]
		[InlineData("Path", 256)]
		[InlineData("Description", 256)]
		public void Project_Create_ExcessiveLength_IsRejected(string propertyName, int length)
		{
			var databasePath = Path.Combine(_directory, "LongProject.pdsx");

			IDataAccessInitializer initializer = new DataAccessInitializer();
			var repository = initializer.InitializePdsx(databasePath);

			var value = new string('A', length);

			var result = repository.Project.Create(
				propertyName == "Name" ? value : "TEST-81346",
				propertyName == "Path" ? value : _directory,
				propertyName == "Description" ? value : "");

			Assert.Null(result.Value);

			var error = Assert.Single(result.Validation.Errors);

			Assert.Equal(propertyName, error.PropertyName);
			Assert.Empty(repository.Project.GetAll());
		}

		[Fact(DisplayName =	"Creating a project accepts maximum field lengths")]
		public void Project_Create_MaximumLengths_IsAccepted()
		{
			var databasePath = Path.Combine(_directory, "MaximumProject.pdsx");

			IDataAccessInitializer initializer = new DataAccessInitializer();
			var repository = initializer.InitializePdsx(databasePath);

			var result = repository.Project.Create(
				new string('N', 50),
				new string('P', 255),
				new string('D', 255));

			Assert.True(result.Validation.IsValid);
			Assert.NotNull(result.Value);

			var stored = repository.Project.GetById(result.Value!.Id);

			Assert.NotNull(stored);
			Assert.Equal(50, stored!.Name.Length);
			Assert.Equal(255, stored.Path.Length);
			Assert.Equal(255, stored.Description.Length);
		}


		[Fact(DisplayName =	"Updating a project persists changes and returns a reusable current version")]
		public void Project_Update_PersistsChangesAndReturnsCurrentVersion()
		{
			var repository = CreateProjectRepository();

			var created = repository.Project.Create(
				"Original",
				_directory,
				"Original description");

			Assert.True(created.Validation.IsValid);
			Assert.NotNull(created.Value);
			Assert.NotNull(created.Value!.LoadedVersion);

			var project = created.Value;
			var originalVersion = project.LoadedVersion;

			project.Name = "Updated";
			project.Description = "Updated description";

			var updated = repository.Project.Update(project);

			Assert.True(updated.Validation.IsValid);
			Assert.NotNull(updated.Value);
			Assert.NotEqual(originalVersion, updated.Value!.LoadedVersion);

			var stored = repository.Project.GetById(project.Id);

			Assert.NotNull(stored);
			Assert.Equal("Updated", stored!.Name);
			Assert.Equal("Updated description", stored.Description);

			// The returned entity carries the version required for another update.
			updated.Value.Description = "Second update";

			var second = repository.Project.Update(updated.Value);

			Assert.True(second.Validation.IsValid);
			Assert.Equal(
				"Second update",
				repository.Project.GetById(project.Id)!.Description);
		}

		[Fact(DisplayName =	"Invalid project changes are rejected without modifying stored data")]
		public void Project_Update_InvalidValues_PreservesStoredData()
		{
			var repository = CreateProjectRepository();
			var created = repository.Project.Create("Original", _directory, "");

			Assert.True(created.Validation.IsValid);

			var project = created.Value!;
			project.Name = "";

			var result = repository.Project.Update(project);

			Assert.Null(result.Value);
			Assert.False(result.Validation.IsValid);
			Assert.Contains(result.Validation.Errors,
				error => error.PropertyName == nameof(IProject.Name));

			Assert.Equal(
				"Original",
				repository.Project.GetById(project.Id)!.Name);
		}

		[Fact(DisplayName =	"An outdated project version cannot overwrite a previously saved change")]
		public void Project_Update_OutdatedVersion_IsRejected()
		{
			var repository = CreateProjectRepository();
			var created = repository.Project.Create("Original", _directory, "");

			Assert.True(created.Validation.IsValid);

			var id = created.Value!.Id;

			// Exercise version capture through both read paths.
			var first = repository.Project.GetById(id)!;
			var second = Assert.Single(repository.Project.GetAll());

			Assert.NotNull(first.LoadedVersion);
			Assert.Equal(first.LoadedVersion, second.LoadedVersion);

			first.Description = "First user's change";
			var firstResult = repository.Project.Update(first);

			Assert.True(firstResult.Validation.IsValid);

			second.Description = "Second user's change";
			var secondResult = repository.Project.Update(second);

			Assert.Null(secondResult.Value);
			Assert.False(secondResult.Validation.IsValid);

			Assert.Equal(
				"First user's change",
				repository.Project.GetById(id)!.Description);
		}

		[Fact(DisplayName =	"A project update without its original version is rejected")]
		public void Project_Update_MissingVersion_IsRejected()
		{
			var repository = CreateProjectRepository();
			var created = repository.Project.Create("Original", _directory, "");

			Assert.True(created.Validation.IsValid);

			var input = new Project
			{
				Id = created.Value!.Id,
				Name = "Changed",
				Path = _directory
			};

			var result = repository.Project.Update(input);

			Assert.Null(result.Value);
			Assert.False(result.Validation.IsValid);

			Assert.Equal(
				"Original",
				repository.Project.GetById(input.Id)!.Name);
		}

		[Fact(DisplayName =	"Updating a missing project returns an identifier validation error")]
		public void Project_Update_MissingRecord_IsRejected()
		{
			var repository = CreateProjectRepository();

			var input = new Project
			{
				Id = Guid.NewGuid(),
				Name = "Missing",
				Path = _directory,
				LoadedVersion = 0
			};

			var result = repository.Project.Update(input);

			Assert.Null(result.Value);

			var error = Assert.Single(result.Validation.Errors);

			Assert.Equal(nameof(IProject.Id), error.PropertyName);
			Assert.Empty(repository.Project.GetAll());
		}

		private (IPdsxRepository Repository, IDatabaseAccess Database) CreateClassificationDatabase()
		{
			var path = Path.Combine(_directory, "Classifications.pdsx");

			IDataAccessInitializer initializer = new DataAccessInitializer();
			var repository = initializer.InitializePdsx(path);

			IDatabaseAccess database = new DatabaseAccess(
				DatabaseConnectionStrings.Create(path));

			return (repository, database);
		}

		private IPdsxRepository CreateProjectRepository()
		{
			IDataAccessInitializer initializer = new DataAccessInitializer();

			return initializer.InitializePdsx(
				Path.Combine(_directory, "ProjectUpdate.pdsx"));
		}


		public void Dispose()
		{
			// Each test owns a separate temporary directory.
			if (Directory.Exists(_directory))
				Directory.Delete(_directory, recursive: true);
		}
	}
}
