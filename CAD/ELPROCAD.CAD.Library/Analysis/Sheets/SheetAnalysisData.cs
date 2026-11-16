using ELPROCAD.CAD.Library.Analysis.Blocks;
using ELPROCAD.CAD.Library.Analysis.Connections;
using ELPROCAD.CAD.Library.Analysis.TitleBlocks;
using ELPROCAD.CAD.Library.CadEntity;

#if ARX_APP
using AcGe = Autodesk.AutoCAD.Geometry;
#elif BRX_APP || PROGECAD_APP
using AcGe = Teigha.Geometry;
#endif

namespace ELPROCAD.CAD.Library.Analysis.Sheets
{
	public sealed class SheetAnalysisData
	{
		public TitleBlockInfo TitleBlock { get; }

		public IReadOnlyList<CadBlock> Blocks { get; }

		public IReadOnlyList<PolylineAnalysisData> Polylines { get; }

		public IReadOnlyList<ConnectionNetwork> Networks { get; }

		internal SheetGrid? Grid { get; set; }

		/// <summary>
		/// Returns null when no ruler exists or the point is outside its grid.
		/// </summary>
		public (string X, string Y)? GetGridCoordinate(AcGe.Point3d position) => Grid?.GetCoordinate(position);

		public SheetAnalysisData(TitleBlockInfo titleBlock, IEnumerable<CadBlock> blocks, 
			IEnumerable<PolylineAnalysisData> polylines, IEnumerable<ConnectionNetwork> networks)
		{
			TitleBlock = titleBlock;

			Blocks = new List<CadBlock>(blocks).AsReadOnly();

			Polylines = new List<PolylineAnalysisData>(polylines).AsReadOnly();

			Networks = new List<ConnectionNetwork>(networks).AsReadOnly();
		}

		public IReadOnlyList<string> GetGridDiagnostics() =>
			Grid?.Describe() ??
			new[] { "GRID: no ruler markers found on the selected sheet." };

		public string DescribeGridPosition(AcGe.Point3d position) =>
			Grid?.DescribePosition(position) ?? "NO_RULER";
	}
}
