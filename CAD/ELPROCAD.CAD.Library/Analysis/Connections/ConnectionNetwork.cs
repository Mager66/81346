using ELPROCAD.CAD.Library.Analysis.Terminals;
#if BRX_APP
using AcDb = Teigha.DatabaseServices;
#endif

namespace ELPROCAD.CAD.Library.Analysis.Connections
{
	public sealed class ConnectionNetwork
	{
		public int Number { get; }

		public IReadOnlyList<AcDb.ObjectId> PolylineIds { get; }

		public IReadOnlyList<BlockTerminal> Terminals { get; }

		public IReadOnlyList<ConnectionJunction> Junctions { get; }

		public ConnectionNetwork(
			int number,
			IEnumerable<AcDb.ObjectId> polylineIds,
			IEnumerable<BlockTerminal> terminals,
			IEnumerable<ConnectionJunction> junctions)
		{
			Number = number;
			PolylineIds = new List<AcDb.ObjectId>(
				polylineIds).AsReadOnly();

			Terminals = new List<BlockTerminal>(
				terminals).AsReadOnly();

			Junctions = new List<ConnectionJunction>(
				junctions).AsReadOnly();
		}
	}
}
