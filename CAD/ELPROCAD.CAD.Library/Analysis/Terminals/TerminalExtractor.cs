using ELPROCAD.CAD.Library.BlockDefinitions;
#if BRX_APP
using AcDb = Teigha.DatabaseServices;
using AcGe = Teigha.Geometry;
#endif

namespace ELPROCAD.CAD.Library.Analysis.Terminals
{
	public sealed class TerminalExtractor
	{
		public IReadOnlyList<BlockTerminal> Extract(AcDb.Transaction transaction, AcDb.ObjectId blockId, BlockDefinition definition)
		{
			if (definition == null)
				throw new ArgumentNullException(nameof(definition));

			if (transaction.GetObject(blockId, AcDb.OpenMode.ForRead) is not AcDb.BlockReference block)
				throw new ArgumentException("Objektet måste vara en blockreferens.", nameof(blockId));			

			var terminals = new List<BlockTerminal>();
			var transform = block.BlockTransform;

			foreach (var terminal in definition.Terminals)
			{
				var offset = new AcGe.Vector3d(
					terminal.Offset.X,
					terminal.Offset.Y,
					0.0);

				var position = block.Position + offset.TransformBy(transform);

				var connectionDirection = GetDirection(terminal.Direction)
					.TransformBy(transform)
					.GetNormal();

				terminals.Add(new BlockTerminal(
					blockId,
					terminal.Number,
					terminal.Direction,
					position,
					connectionDirection));
			}
			return terminals.AsReadOnly();
		}

		private static AcGe.Vector3d GetDirection(int direction)
		{
			return direction switch
			{
				0 => new AcGe.Vector3d(-1.0, 0.0, 0.0),
				1 => new AcGe.Vector3d(1.0, 0.0, 0.0),
				2 => new AcGe.Vector3d(0.0, 1.0, 0.0),
				3 => new AcGe.Vector3d(0.0, -1.0, 0.0),
				_ => throw new ArgumentOutOfRangeException(
					nameof(direction),
					direction,
					"Uttagsriktningen måste vara mellan 0 och 3.")
			};
		}
	}
}
