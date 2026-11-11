using ELPROCAD.CAD.Library.Analysis.Blocks;
using ELPROCAD.CAD.Library.Analysis.Connections;
using ELPROCAD.CAD.Library.Analysis.Terminals;
using ELPROCAD.CAD.Library.Analysis.TitleBlocks;
using ELPROCAD.CAD.Library.BlockDefinitions;
using ELPROCAD.CAD.Library.CadEntity;


#if BRX_APP
using AcDb = Teigha.DatabaseServices;
#endif


namespace ELPROCAD.CAD.Library.Analysis.Sheets
{
	public sealed class SheetAnalysisService
	{
		private readonly SheetEntityReader entityReader;
		private readonly BlockReader blockReader;
		private readonly PolylineReader polylineReader;
		private readonly TerminalEndpointMatcher endpointMatcher;
		private readonly TerminalDirectionMatcher directionMatcher;
		private readonly JunctionReader junctionReader;
		private readonly JunctionMatcher junctionMatcher;
		private readonly ConnectionNetworkBuilder networkBuilder;

		public SheetAnalysisService(BlockDefinitionLookup definitionLookup,	double distanceTolerance, double angularToleranceRadians)
		{
			if (definitionLookup == null)
				throw new ArgumentNullException(nameof(definitionLookup));

			entityReader = new SheetEntityReader();

			blockReader = new BlockReader(definitionLookup, new TerminalExtractor());

			polylineReader = new PolylineReader(distanceTolerance);
			endpointMatcher = new TerminalEndpointMatcher(distanceTolerance);

			directionMatcher = new TerminalDirectionMatcher(
				angularToleranceRadians);

			junctionReader = new JunctionReader();
			junctionMatcher = new JunctionMatcher(distanceTolerance);
			networkBuilder = new ConnectionNetworkBuilder();
		}

		public SheetAnalysisData Analyze(AcDb.Transaction transaction, AcDb.ObjectId spaceId, TitleBlockInfo titleBlock)
		{
			if (transaction == null)
				throw new ArgumentNullException(nameof(transaction));

			if (titleBlock == null)
				throw new ArgumentNullException(nameof(titleBlock));

			var entities = entityReader.Read(
				transaction,
				spaceId,
				titleBlock.SheetLayer);

			var blocks = ReadBlocks(transaction, entities);
			var polylines = ReadPolylines(transaction, entities);

			var junctions = junctionReader.Read(transaction, blocks);

			var junctionBlockIds = new HashSet<AcDb.ObjectId>(
				junctions.Select(junction => junction.BlockId));

			var terminals = blocks
				.Where(block =>
					block.HasDefinition &&
					!junctionBlockIds.Contains(block.ObjectId))
				.SelectMany(block => block.Terminals)
				.ToList();

			var terminalConnections = FindTerminalConnections(transaction, polylines, terminals);

			var junctionMatches = new List<JunctionPolylineMatch>();

			foreach (var junction in junctions)
			{
				junctionMatches.AddRange(junctionMatcher.Match(
					transaction,
					junction,
					polylines));
			}

			var networks = networkBuilder.Build(
				polylines,
				terminalConnections,
				junctionMatches);

			return new SheetAnalysisData(
				titleBlock,
				blocks,
				polylines,
				networks);
		}

		private List<CadBlock> ReadBlocks(AcDb.Transaction transaction, SheetEntities entities)
		{
			var blocks = new List<CadBlock>();

			foreach (var blockId in entities.BlockIds)
			{
				var block = blockReader.Read(transaction, blockId);

				if (!string.Equals(
					block.BlockName,
					"EPCB000", // TODO: No fixed names for forms
					StringComparison.OrdinalIgnoreCase))
				{
					blocks.Add(block);
				}
			}

			return blocks;
		}

		private List<PolylineAnalysisData> ReadPolylines(AcDb.Transaction transaction, SheetEntities entities)
		{
			var polylines = new List<PolylineAnalysisData>();

			foreach (var polylineId in entities.PolylineIds)
			{
				var polyline = polylineReader.Read(
					transaction,
					polylineId);

				if (polyline != null)
					polylines.Add(polyline);
			}

			return polylines;
		}

		private List<TerminalEndpointMatch> FindTerminalConnections(AcDb.Transaction transaction, IEnumerable<PolylineAnalysisData> polylines, IReadOnlyList<BlockTerminal> terminals)
		{
			var connections = new List<TerminalEndpointMatch>();

			foreach (var polyline in polylines)
			{
				var candidates = endpointMatcher.Match(
					polyline,
					terminals);

				foreach (var candidate in candidates)
				{
					if (directionMatcher.Matches(transaction, candidate))
						connections.Add(candidate);
				}
			}

			return connections;
		}
	}
}
