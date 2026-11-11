#if BRX_APP
using ELPROCAD.CAD.Library.Analysis.Blocks;
using ELPROCAD.CAD.Library.CadEntity;
using AcDb = Teigha.DatabaseServices;
#endif

namespace ELPROCAD.CAD.Library.Analysis.Connections
{
	public sealed class JunctionReader
	{
		private const string JunctionBlockName = "EPCB001";

		public IReadOnlyList<ConnectionJunction> Read(AcDb.Transaction transaction, IEnumerable<CadBlock> blocks)
		{
			if (transaction == null)
				throw new ArgumentNullException(nameof(transaction));

			if (blocks == null)
				throw new ArgumentNullException(nameof(blocks));

			var junctions = new List<ConnectionJunction>();

			foreach (var block in blocks)
			{
				if (!string.Equals(
					block.BlockName,
					JunctionBlockName,
					StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}

				if (transaction.GetObject(block.ObjectId, AcDb.OpenMode.ForRead) is not AcDb.BlockReference reference)				
					throw new InvalidOperationException("Förgreningspunkten måste vara en blockreferens.");


				junctions.Add(new ConnectionJunction(block.ObjectId, reference.Position));
			}

			return junctions.AsReadOnly();
		}		
	}
}
