#if BRX_APP
using AcDb = Teigha.DatabaseServices;
using AcGe = Teigha.Geometry;
#endif

namespace ELPROCAD.CAD.Library.Analysis.Connections
{
	public sealed class ConnectionJunction
	{
		public AcDb.ObjectId BlockId { get; }

		public AcGe.Point3d Position { get; }

		public ConnectionJunction(AcDb.ObjectId blockId, AcGe.Point3d position)
		{
			BlockId = blockId;
			Position = position;
		}
	}
}
