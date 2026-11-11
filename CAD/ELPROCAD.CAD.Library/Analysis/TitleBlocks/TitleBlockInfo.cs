#if BRX_APP
using AcDb = Teigha.DatabaseServices;
#endif

namespace ELPROCAD.CAD.Library.Analysis.TitleBlocks
{
	public sealed class TitleBlockInfo
	{
		public AcDb.ObjectId ObjectId { get; set; }

		public string SheetLayer { get; set; } = string.Empty;
	}
}
