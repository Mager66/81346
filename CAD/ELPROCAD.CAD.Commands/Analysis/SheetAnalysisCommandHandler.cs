using ELPROCAD.CAD.Data.Analysis;
using ELPROCAD.CAD.Library.Analysis.Sheets;
using Microsoft.Extensions.DependencyInjection;
using Teigha.Geometry;


#if BRX_APP
using AcAp = Bricscad.ApplicationServices;
#endif

namespace ELPROCAD.CAD.Commands.Analysis
{
	internal sealed class SheetAnalysisCommandHandler
	{
		private const string DefinitionFilePath = @"C:\Program Files\ELPROCAD\ELPROCAD Enterprise\CAD\EPCB BlockTable.json";

		private const double DistanceTolerance = 0.001;
		private const double AngularToleranceRadians = Math.PI / 180.0;

		private readonly IServiceProvider _serviceProvider;

		public SheetAnalysisCommandHandler(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

		public void Execute()
		{
			var doc = AcAp.Application.DocumentManager.MdiActiveDocument;
			if(doc == null)
				return;

			var ed = doc.Editor;

			try
			{
				if(!SheetSelectionStore.TryGet(doc, out var titleBlockId))
				{
					ed.WriteMessage("\nIngen blankett är vald.");
					return;
				}

				var runner = new SheetAnalysisRunner();

				var result = runner.Run(
					doc.Database,
					titleBlockId,
					DefinitionFilePath,
					DistanceTolerance,
					AngularToleranceRadians);

				using(var scope = _serviceProvider.CreateScope())
				{
					var importer = scope.ServiceProvider.GetRequiredService<AnalysisBlockImporter>();
					var importResult = importer.Import(doc, result);

					var treeReader = scope.ServiceProvider.GetRequiredService<LocationTreeReader>();
					var lines = treeReader.ReadLines();

					ed.WriteMessage($"\nStored location tree ({lines.Count} nodes):");

					foreach (var line in lines)
						ed.WriteMessage("\n" + line);

					ed.WriteMessage("\nStored document and object associations:");

					foreach (var line in treeReader.ReadObjectLines())
						ed.WriteMessage("\n" + line);

					ed.WriteMessage(
						$"\nLocation nodes created: {importResult.CreatedLocations}" +
						$"\nBlocks processed: {importResult.ProcessedBlocks}" +
						$"\nImport errors: {importResult.Errors.Count}");

					foreach (var error in importResult.Errors)
					{
						ed.WriteMessage(
							$"\nBlock {error.BlockId.Handle}: {error.Message}");
					}
				}

				ed.WriteMessage(
					$"\nBlad/lager: {result.TitleBlock.SheetLayer}" +
					$"\nBlock: {result.Blocks.Count}" +
					$"\nUttag: {result.Blocks.Sum(block => block.Terminals.Count)}" +
					$"\nÖppna polylines: {result.Polylines.Count(polyline => !polyline.IsClosed)}" +
					$"\nSlutna polylines: {result.Polylines.Count(polyline => polyline.IsClosed)}" +
					$"\nLedningsnät: {result.Networks.Count}");

				foreach (var network in result.Networks)
				{
					var terminals = network.Terminals.Select(terminal => $"{terminal.BlockId.Handle}:{terminal.Number}");

					ed.WriteMessage(
						$"\nNät {network.Number}: " +
						$"{network.PolylineIds.Count} ledare, " +
						$"{network.Junctions.Count} förgreningspunkter, " +
						$"uttag [{string.Join(", ", terminals)}]");
				}

			}
			catch (Exception exception)
			{
				ed.WriteMessage($"\nAnalysen kunde inte slutföras: {exception.Message}");
			}
		}
	}
}
