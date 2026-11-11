using ELPROCAD.CAD.Library.Analysis.Terminals;
#if BRX_APP
using AcDb = Teigha.DatabaseServices;
#endif

namespace ELPROCAD.CAD.Library.Analysis.Connections
{
	public sealed class TerminalEndpointMatch
	{
		public AcDb.ObjectId PolylineId { get; }

		public bool IsStartPoint { get; }

		public BlockTerminal Terminal { get; }

		public TerminalEndpointMatch(AcDb.ObjectId polylineId, bool isStartPoint, BlockTerminal terminal)
		{
			PolylineId = polylineId;
			IsStartPoint = isStartPoint;
			Terminal = terminal;
		}
	}
}
