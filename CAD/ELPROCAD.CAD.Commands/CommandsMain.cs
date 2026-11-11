using DevExpress.Xpo;
using ELPROCAD.CAD.Commands.Analysis;
using ELPROCAD.CAD.Data;
using ELPROCAD.CAD.Data.Analysis;
using ELPROCAD.CAD.Data.CRUD.Classification;
using ELPROCAD.CAD.Data.Model.Classification;
using ELPROCAD.CAD.Data.Model.Tests;
using ELPROCAD.CAD.Data.Provider;
using ELPROCAD.CAD.Library.Analysis.Sheets;
using ELPROCAD.CAD.Library.Analysis.TitleBlocks;
using ELPROCAD.CAD.TestWPF;
using Microsoft.Extensions.DependencyInjection;
using System.Data.Common;
using System.IO;
using VistaDB;



#if BRX_APP
using AcAp = Bricscad.ApplicationServices;
using AcDb = Teigha.DatabaseServices;
using AcTrx = Teigha.Runtime;
#endif

[assembly: AcTrx.ExtensionApplication(typeof(ELPROCAD.CAD.Commands.CommandsMain))]
[assembly: AcTrx.CommandClass(typeof(ELPROCAD.CAD.Commands.CommandsMain))]

namespace ELPROCAD.CAD.Commands
{
	public sealed class CommandsMain : AcTrx.IExtensionApplication
	{
		private static ServiceProvider? _serviceProvider;
		const string projectDirectory = @"C:\Dev\POC\81346\TestProject";


		public void Initialize()
		{
			AcAp.Application.DocumentManager.DocumentBecameCurrent += DocumentManager_DocumentBecameCurrent;
			ConnectToDb();
		}

		private void DocumentManager_DocumentBecameCurrent(object sender, AcAp.DocumentCollectionEventArgs e)
		{
			var doc = AcAp.Application.DocumentManager.MdiActiveDocument;
			if (doc == null)
				return;

			if (SheetSelectionStore.TryGet(doc, out _))
				return;

			using var tr = doc.Database.TransactionManager.StartTransaction();

			var bt = (AcDb.BlockTable)tr.GetObject(doc.Database.BlockTableId, AcDb.OpenMode.ForRead);

			var reader = new TitleBlockReader();

			var titleBlocks = reader.Read(tr, bt[AcDb.BlockTableRecord.ModelSpace]);

			TitleBlockInfo? selected = null;

			if (titleBlocks.Count == 1)
				selected = titleBlocks[0];
			else if (titleBlocks.Count > 1)
			{
				var clayer = (AcDb.LayerTableRecord)tr.GetObject(doc.Database.Clayer, AcDb.OpenMode.ForRead);

				var matches = titleBlocks
					.Where(block => string.Equals(
						block.SheetLayer,
						clayer.Name,
						StringComparison.OrdinalIgnoreCase))
					.ToList();

				if (matches.Count == 1)
					selected = matches[0];
			}
			tr.Commit();

			SheetSelectionStore.Clear(doc.Database);

			if (selected != null)
				SheetSelectionStore.Set(doc, selected.ObjectId);

		}

		public void Terminate()
		{
			_serviceProvider?.Dispose();
			_serviceProvider = null;
		}

		[AcTrx.CommandMethod("ELP_ANALYZE", AcTrx.CommandFlags.Modal)]
		public static void AnalyzeSheet()
		{
			if (_serviceProvider == null)
				throw new InvalidOperationException("Databasåtkomsten är inte initierad.");

			new SheetAnalysisCommandHandler(_serviceProvider).Execute();
		}


		private static void ConnectToDb()
		{
			Directory.CreateDirectory(projectDirectory);
			var dbPath = Path.Combine(projectDirectory, "ELPROCAD.pdsx");

			var connectionStringBuilder = new DbConnectionStringBuilder
			{
				["XpoProvider"] = "VistaDB6",
				["Data Source"] = dbPath,
				["Open Mode"] = VistaDBDatabaseOpenMode.MultiProcessReadWrite.ToString()
			};

			var services = new ServiceCollection();

			services.AddElprocadDataAccess(connectionStringBuilder.ConnectionString);

			services.AddAnalysisDataAccess(Path.Combine(projectDirectory, "AnalysisTest"));

			_serviceProvider = services.BuildServiceProvider();

			using (var scope = _serviceProvider.CreateScope())
			{
				try
				{
					scope.ServiceProvider
						.GetRequiredService<DatabaseInitializer>()
						.Initialize();
				}
				catch (Exception ex)
				{

				}
			}
		}

		[AcTrx.CommandMethod("TestValidations")]
		public static void TestValidations()
		{
			var queryMain = _serviceProvider?.GetRequiredService<QueryMainClassification>();

			queryMain.Create(abbrevation: "ABC", tooltip: "Kurres tips!", image: "");

			var win = new MainWindow(queryMain);
			AcAp.Application.ShowModelessWindow(AcAp.Application.MainWindow.Handle, win);
		}

		[AcTrx.CommandMethod("TEST_POCO")]
		public static void TestPoco()
		{
			var scopeFactory = _serviceProvider
				.GetRequiredService<IServiceScopeFactory>();

			var test = new PocoPersistenceTest(scopeFactory);

			//var result = test.Run();
			var result = test.RunConcurrency();

			AcAp.Application.DocumentManager.MdiActiveDocument
				.Editor.WriteMessage("\n" + result);
		}
	}
}
