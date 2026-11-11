#if BRX_APP
using AcDb = Teigha.DatabaseServices;
#endif


namespace ELPROCAD.CAD.Library.Analysis.Connections
{
	public sealed class JunctionPolylineMatch
	{
		public ConnectionJunction Junction { get; }

		public AcDb.ObjectId PolylineId { get; }

		public bool AtStartPoint { get; }

		public bool AtEndPoint { get; }

		public bool IsInterior => !AtStartPoint && !AtEndPoint;

		public JunctionPolylineMatch(ConnectionJunction junction, AcDb.ObjectId polylineId, bool atStartPoint, bool atEndPoint)
		{
			Junction = junction;
			PolylineId = polylineId;
			AtStartPoint = atStartPoint;
			AtEndPoint = atEndPoint;
		}
	}
}

