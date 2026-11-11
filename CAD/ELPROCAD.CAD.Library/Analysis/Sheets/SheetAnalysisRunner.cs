using ELPROCAD.CAD.Library.Analysis.TitleBlocks;
using ELPROCAD.CAD.Library.BlockDefinitions;

#if BRX_APP
using AcDb = Teigha.DatabaseServices;
#endif

namespace ELPROCAD.CAD.Library.Analysis.Sheets
{
	public sealed class SheetAnalysisRunner
	{
		public SheetAnalysisData Run(AcDb.Database database, AcDb.ObjectId titleBlockId, string definitionFilePath, double distanceTolerance, double angularToleranceRadians)
		{
			if (database == null)
				throw new ArgumentNullException(nameof(database));

			if (titleBlockId.IsNull ||
				!titleBlockId.IsValid ||
				titleBlockId.IsErased)
			{
				throw new ArgumentException(
					"Blankettens objekt-ID är inte giltigt.",
					nameof(titleBlockId));
			}

			if (titleBlockId.Database != database)
			{
				throw new ArgumentException(
					"Blanketten tillhör inte den angivna ritningen.",
					nameof(titleBlockId));
			}

			var definitions = BlockDefinitionParser.Load(definitionFilePath);

			var lookup = new BlockDefinitionLookup(definitions);

			var analysisService = new SheetAnalysisService(lookup, distanceTolerance, angularToleranceRadians);

			using var transaction = database.TransactionManager.StartTransaction();

			if (transaction.GetObject(titleBlockId, AcDb.OpenMode.ForRead) is not AcDb.BlockReference block)
			{
				throw new ArgumentException(
					"Blanketten måste vara en blockreferens.",
					nameof(titleBlockId));
			}

			var spaceId = block.OwnerId;
			var titleBlockReader = new TitleBlockReader();

			var titleBlock = titleBlockReader
				.Read(transaction, spaceId)
				.FirstOrDefault(candidate =>
					candidate.ObjectId == titleBlockId);

			if (titleBlock == null)
			{
				throw new InvalidOperationException(
					"Det angivna blocket är inte en EPCB000-blankett.");
			}

			var result = analysisService.Analyze(
				transaction,
				spaceId,
				titleBlock);

			transaction.Commit();

			return result;
		}
	}
}
