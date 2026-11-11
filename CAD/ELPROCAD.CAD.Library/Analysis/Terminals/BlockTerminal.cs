#if BRX_APP
using AcDb = Teigha.DatabaseServices;
using AcGe = Teigha.Geometry;
#endif

namespace ELPROCAD.CAD.Library.Analysis.Terminals
{
	public sealed class BlockTerminal
	{
		public AcDb.ObjectId BlockId { get; }

		public int Number { get; }

		public int Direction { get; }

		public AcGe.Point3d Position { get; }

		public AcGe.Vector3d ConnectionDirection { get; }

		public BlockTerminal(
			AcDb.ObjectId blockId,
			int number,
			int direction,
			AcGe.Point3d position,
			AcGe.Vector3d connectionDirection)
		{
			BlockId = blockId;
			Number = number;
			Direction = direction;
			Position = position;
			ConnectionDirection = connectionDirection;
		}
	}
}
