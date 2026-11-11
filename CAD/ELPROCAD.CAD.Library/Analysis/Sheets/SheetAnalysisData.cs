using ELPROCAD.CAD.Library.Analysis.Blocks;
using ELPROCAD.CAD.Library.Analysis.Connections;
using ELPROCAD.CAD.Library.Analysis.TitleBlocks;
using ELPROCAD.CAD.Library.CadEntity;

namespace ELPROCAD.CAD.Library.Analysis.Sheets
{
	public sealed class SheetAnalysisData
	{
		public TitleBlockInfo TitleBlock { get; }

		public IReadOnlyList<CadBlock> Blocks { get; }

		public IReadOnlyList<PolylineAnalysisData> Polylines { get; }

		public IReadOnlyList<ConnectionNetwork> Networks { get; }

		public SheetAnalysisData(TitleBlockInfo titleBlock, IEnumerable<CadBlock> blocks, 
			IEnumerable<PolylineAnalysisData> polylines, IEnumerable<ConnectionNetwork> networks)
		{
			TitleBlock = titleBlock;

			Blocks = new List<CadBlock>(blocks).AsReadOnly();

			Polylines = new List<PolylineAnalysisData>(polylines).AsReadOnly();

			Networks = new List<ConnectionNetwork>(networks).AsReadOnly();
		}
	}
}
