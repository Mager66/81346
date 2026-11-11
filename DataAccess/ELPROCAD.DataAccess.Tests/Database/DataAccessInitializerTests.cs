
using DevExpress.Xpo;
using DevExpress.Xpo.DB;
using ELPROCAD.DataAccess.Tests.Models;
using System.Data.Common;
using Xunit;

namespace ELPROCAD.DataAccess.Tests.Database
{
	public sealed class DataAccessInitializerTests : IDisposable
	{
		private readonly string _directory;
		private readonly string _databasePath;

		public DataAccessInitializerTests()
		{
			_directory = Path.Combine(
				Path.GetTempPath(),
				"ELPROCAD.DataAccess.Tests",
				Guid.NewGuid().ToString("N"));

			_databasePath = Path.Combine(
				_directory,
				"Project.pdsx");
		}

		[Fact]
		public void Initialize_NewDatabase_CreatesUsableSchema()
		{
			var initializer = new DataAccessInitializer();

			initializer.Initialize(
				_databasePath,
				typeof(InitializationTestEntity));

			Assert.True(File.Exists(_databasePath));

			var id = Guid.NewGuid();

			using (var dataLayer = OpenExisting())
			using (var unitOfWork = new UnitOfWork(dataLayer))
			{
				unitOfWork.Save(new InitializationTestEntity
				{
					Id = id,
					Name = "Första posten"
				});

				unitOfWork.CommitChanges();
			}

			using (var dataLayer = OpenExisting())
			using (var unitOfWork = new UnitOfWork(dataLayer))
			{
				var stored =
					unitOfWork.GetObjectByKey<InitializationTestEntity>(id);

				Assert.NotNull(stored);
				Assert.Equal("Första posten", stored!.Name);
			}
		}

		[Fact]
		public void Initialize_ExistingDatabase_PreservesData()
		{
			var initializer = new DataAccessInitializer();

			initializer.Initialize(
				_databasePath,
				typeof(InitializationTestEntity));

			var id = Guid.NewGuid();

			using (var dataLayer = OpenExisting())
			using (var unitOfWork = new UnitOfWork(dataLayer))
			{
				unitOfWork.Save(new InitializationTestEntity
				{
					Id = id,
					Name = "Ska finnas kvar"
				});

				unitOfWork.CommitChanges();
			}

			// Initiera samma databas igen.
			initializer.Initialize(
				_databasePath,
				typeof(InitializationTestEntity));

			using (var dataLayer = OpenExisting())
			using (var unitOfWork = new UnitOfWork(dataLayer))
			{
				var stored =
					unitOfWork.GetObjectByKey<InitializationTestEntity>(id);

				Assert.NotNull(stored);
				Assert.Equal("Ska finnas kvar", stored!.Name);
			}
		}

		private IDataLayer OpenExisting()
		{
			var connectionString = new DbConnectionStringBuilder
			{
				["XpoProvider"] = "VistaDB6",
				["Data Source"] = _databasePath,
				["Open Mode"] = "MultiProcessReadWrite",
				["Pooling"] = false
			};

			// Tester ska inte råka skapa eller reparera schemat
			// som initializer ansvarar för.
			return XpoDefault.GetDataLayer(
				connectionString.ConnectionString,
				AutoCreateOption.None);
		}


		public void Dispose()
		{
			if (Directory.Exists(_directory))
				Directory.Delete(_directory, recursive: true);
		}
	}
}
