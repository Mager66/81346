using ELPROCAD.CAD.Library.Analysis.Terminals;
#if BRX_APP
using AcDb = Teigha.DatabaseServices;
#endif

namespace ELPROCAD.CAD.Library.Analysis.Blocks
{
	[Obsolete("Use CadBlock instead")]
	public sealed class BlockAnalysisData
	{
		public AcDb.ObjectId BlockId { get; }

		public string Name { get; }

		public int? ObjectType { get; }

		public bool HasDefinition => ObjectType.HasValue;

		public IReadOnlyList<BlockTerminal> Terminals { get; }

		public BlockAnalysisData(
			AcDb.ObjectId blockId,
			string name,
			int? objectType,
			IEnumerable<BlockTerminal> terminals)
		{
			BlockId = blockId;
			Name = name;
			ObjectType = objectType;
			Terminals = new List<BlockTerminal>(terminals).AsReadOnly();
		}
	}
}
