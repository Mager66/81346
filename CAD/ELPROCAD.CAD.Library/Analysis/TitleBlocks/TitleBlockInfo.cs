#if BRX_APP
using AcDb = Teigha.DatabaseServices;
#endif

namespace ELPROCAD.CAD.Library.Analysis.TitleBlocks
{
	public sealed class TitleBlockInfo
	{
		public AcDb.ObjectId ObjectId { get; set; }

		public string SheetLayer { get; set; } = string.Empty;

		public string SheetNo { get; set; } = string.Empty;

		public bool IsMultiSheet { get; set; }

		/// <summary>
		/// Tests top-level entity layers for the selected sheet.
		/// </summary>
		public bool IncludesLayer(string layer) => IsMultiSheet
			? string.Equals(layer, SheetLayer, StringComparison.OrdinalIgnoreCase)
			: !string.Equals(layer, "0", StringComparison.OrdinalIgnoreCase);
	}
}
