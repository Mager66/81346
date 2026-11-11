using DevExpress.Xpo;
using DevExpress.Xpo.DB;
using ELPROCAD.DataAccess.Changes.Locations;
using ELPROCAD.DataAccess.CRUD;
using ELPROCAD.DataAccess.Database;
using ELPROCAD.DataAccess.Database.Migrations;
using ELPROCAD.DataAccess.Models.MDBX.Aspects;
using ELPROCAD.DataAccess.Models.MDBX.Documents;
using ELPROCAD.DataAccess.Models.MDBX.Objects;
using ELPROCAD.DataAccess.Models.PDSX.Classification;
using ELPROCAD.DataAccess.Models.PDSX.Documents;
using ELPROCAD.DataAccess.Models.PDSX.ProjectData;
using ELPROCAD.DataAccess.Repositories.MDBX;
using ELPROCAD.DataAccess.Repositories.MDBX.Aspects;
using ELPROCAD.DataAccess.Repositories.MDBX.Documents;
using ELPROCAD.DataAccess.Repositories.MDBX.Objects;
using ELPROCAD.DataAccess.Repositories.PDSX;
using ELPROCAD.DataAccess.Repositories.PDSX.Classification;
using ELPROCAD.DataAccess.Repositories.PDSX.Documents;
using ELPROCAD.DataAccess.Repositories.PDSX.ProjectData;

namespace ELPROCAD.DataAccess
{
	internal sealed class DataAccessInitializer : IDataAccessInitializer
	{
		public IPdsxRepository InitializePdsx(string databasePath)
		{
			// Resolve the path once for initialization and subsequent access.
			var connectionString = DatabaseConnectionStrings.Create(databasePath);
			var fullPath = Path.GetFullPath(databasePath);

			InitializeDatabase(fullPath, connectionString, new[] 
			{
				typeof(Project),
				typeof(MainClass), 
				typeof(SubClass),
				typeof(FileFormat),
				typeof(OpeningProgram),
				typeof(FileFormatProgram)
			},
			PdsxDefaults.Initialize);

			var database = new DatabaseAccess(connectionString);
			var read = new Read(database);
			var create = new Create(database);
			var update = new Update(database);
			var delete = new Delete(database);

			return new PdsxRepository(
				new MainClassRepository(read),
				new SubClassRepository(read),
				new ProjectRepository(read, create, update),
				new FileFormatRepository(read, create, update),
				new OpeningProgramRepository(read, create, update),
				new FileFormatProgramRepository(read, create, update, delete));
		}

		public IMdbxRepository InitializeMdbx(string databasePath)
		{
			var connectionString = DatabaseConnectionStrings.Create(databasePath);
			var fullPath = Path.GetFullPath(databasePath);

			InitializeDatabase(
				fullPath,
				connectionString,
				new[] 
				{
					typeof(Document),
					typeof(Sheet),
					typeof(CadDocumentSpace),
					typeof(DocumentProperties),
					typeof(CadDocumentCustomProperty),
					typeof(EngineeringObject),
					typeof(CadSymbolLink),
					typeof(PowerFunction),
					typeof(ControlFunction),
					typeof(TopographicStructure),
					typeof(Location),
					typeof(Function),
					typeof(Product)
				});

			var database = new DatabaseAccess(connectionString);

			// Migrate existing links after updating the schema.
			// Execute provides a separate unit of work for this operation.
			database.Execute(unitOfWork =>
			{
				CadSymbolLinkSheetMigration.Apply(unitOfWork);
			});

			var read = new Read(database);
			var create = new Create(database);
			var update = new Update(database);
			var delete = new Delete(database);

			return new MdbxRepository(
				new DocumentRepository(read, create, update, delete),
				new SheetRepository(read, create, update, delete),
				new CadDocumentSpaceRepository(read),
				new DocumentPropertiesRepository(read),
				new CadDocumentCustomPropertyRepository(read),
				new LocationRepository(read, create, update, delete, new MoveLocation(database), new LocationChangeWriter(database)),
				new EngineeringObjectRepository(read, create, update),
				new CadSymbolLinkRepository(read, create, delete));
		}

		/// <summary>
		/// Initializes a database with an explicitly supplied schema.
		/// Used internally and by integration tests.
		/// </summary>
		internal void Initialize(string databasePath, params Type[] entityTypes)
		{
			var connectionString = DatabaseConnectionStrings.Create(databasePath);

			if (entityTypes == null)
				throw new ArgumentNullException(nameof(entityTypes));

			if (entityTypes.Length == 0 || entityTypes.Any(type => type == null))
			{
				throw new ArgumentException(
					"At least one valid entity type is required.",
					nameof(entityTypes));
			}

			InitializeDatabase(
				Path.GetFullPath(databasePath),
				connectionString,
				entityTypes);
		}

		private static void InitializeDatabase(string databasePath,	string connectionString, Type[] entityTypes,	Action<UnitOfWork>? initializeNewDatabase = null)
		{
			var directory = Path.GetDirectoryName(databasePath);

			if (!string.IsNullOrEmpty(directory))
				Directory.CreateDirectory(directory);

			var isNewDatabase = !File.Exists(databasePath);

			var autoCreateOption = isNewDatabase
				? AutoCreateOption.DatabaseAndSchema
				: AutoCreateOption.SchemaOnly;

			using var dataLayer = XpoDefault.GetDataLayer(
				connectionString,
				autoCreateOption);

			using var unitOfWork = new UnitOfWork(dataLayer);

			unitOfWork.UpdateSchema(entityTypes);

			if (isNewDatabase && initializeNewDatabase != null)
			{
				initializeNewDatabase(unitOfWork);
				unitOfWork.CommitChanges();
			}
		}
	}
}
