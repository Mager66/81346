#if BRX_APP
using AcDb = Teigha.DatabaseServices;
#endif

namespace ELPROCAD.CAD.Library.Analysis.Connections
{
	public sealed class ConnectionNetworkBuilder
	{
		public IReadOnlyList<ConnectionNetwork> Build(
		IEnumerable<PolylineAnalysisData> polylines,
		IEnumerable<TerminalEndpointMatch> terminalConnections,
		IEnumerable<JunctionPolylineMatch> junctionMatches)
		{
			if (polylines == null)
				throw new ArgumentNullException(nameof(polylines));

			if (terminalConnections == null)
				throw new ArgumentNullException(nameof(terminalConnections));

			if (junctionMatches == null)
				throw new ArgumentNullException(nameof(junctionMatches));

			var wireIds = polylines
				.Where(polyline => !polyline.IsClosed)
				.Select(polyline => polyline.ObjectId)
				.Distinct()
				.ToList();

			var parents = wireIds.ToDictionary(
				wireId => wireId,
				wireId => wireId);

			var terminals = terminalConnections
				.Where(match => parents.ContainsKey(match.PolylineId))
				.ToList();

			var junctions = junctionMatches
				.Where(match => parents.ContainsKey(match.PolylineId))
				.ToList();

			var terminalGroups = terminals.GroupBy(match => new
			{
				match.Terminal.BlockId,
				match.Terminal.Number
			});

			foreach (var group in terminalGroups)
			{
				JoinAll(
					parents,
					group.Select(match => match.PolylineId));
			}

			foreach (var group in junctions.GroupBy(
				match => match.Junction.BlockId))
			{
				var hasEndpoint = group.Any(
					match => match.AtStartPoint || match.AtEndPoint);

				if (!hasEndpoint)
					continue;

				JoinAll(
					parents,
					group.Select(match => match.PolylineId));
			}

			var networks = new List<ConnectionNetwork>();

			foreach (var group in wireIds.GroupBy(
				wireId => Find(parents, wireId)))
			{
				var networkWireIds = group.ToList();
				var networkWireSet =
					new HashSet<AcDb.ObjectId>(networkWireIds);

				var networkTerminals = terminals
					.Where(match => networkWireSet.Contains(match.PolylineId))
					.Select(match => match.Terminal)
					.GroupBy(terminal => new
					{
						terminal.BlockId,
						terminal.Number
					})
					.Select(terminalGroup => terminalGroup.First())
					.ToList();

				var networkJunctions = junctions
					.Where(match => networkWireSet.Contains(match.PolylineId))
					.Select(match => match.Junction)
					.GroupBy(junction => junction.BlockId)
					.Select(junctionGroup => junctionGroup.First())
					.ToList();

				networks.Add(new ConnectionNetwork(
					networks.Count + 1,
					networkWireIds,
					networkTerminals,
					networkJunctions));
			}

			return networks.AsReadOnly();
		}

		private static void JoinAll(Dictionary<AcDb.ObjectId, AcDb.ObjectId> parents, IEnumerable<AcDb.ObjectId> wireIds)
		{
			using var enumerator = wireIds.Distinct().GetEnumerator();

			if (!enumerator.MoveNext())
				return;

			var firstRoot = Find(parents, enumerator.Current);

			while (enumerator.MoveNext())
			{
				var nextRoot = Find(parents, enumerator.Current);

				if (nextRoot != firstRoot)
					parents[nextRoot] = firstRoot;
			}
		}

		private static AcDb.ObjectId Find(Dictionary<AcDb.ObjectId, AcDb.ObjectId> parents,	AcDb.ObjectId wireId)
		{
			var root = wireId;

			while (parents[root] != root)
				root = parents[root];

			while (parents[wireId] != wireId)
			{
				var next = parents[wireId];
				parents[wireId] = root;
				wireId = next;
			}

			return root;
		}
	}
}
