#if BRX_APP
using AcDb = Teigha.DatabaseServices;
#endif

namespace ELPROCAD.CAD.Library.Analysis.Sheets
{
	public sealed class SheetEntities
	{
		public string LayerName { get; }

		public IReadOnlyList<AcDb.ObjectId> BlockIds { get; }

		public IReadOnlyList<AcDb.ObjectId> PolylineIds { get; }

		public SheetEntities(string layerName, IEnumerable<AcDb.ObjectId> blockIds, IEnumerable<AcDb.ObjectId> polylineIds)
		{
			LayerName = layerName;
			BlockIds = new List<AcDb.ObjectId>(blockIds).AsReadOnly();
			PolylineIds = new List<AcDb.ObjectId>(polylineIds).AsReadOnly();
		}
	}
}
