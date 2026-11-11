#if BRX_APP
using AcDb = Teigha.DatabaseServices;
using AcGe = Teigha.Geometry;
#endif

namespace ELPROCAD.CAD.Library.Analysis.Connections
{
	public sealed class PolylineAnalysisData
	{
		public AcDb.ObjectId ObjectId { get; }

		public AcGe.Point3d StartPoint { get; }

		public AcGe.Point3d EndPoint { get; }

		public bool IsClosed { get; }

		public PolylineAnalysisData(
			AcDb.ObjectId objectId,
			AcGe.Point3d startPoint,
			AcGe.Point3d endPoint,
			bool isClosed)
		{
			ObjectId = objectId;
			StartPoint = startPoint;
			EndPoint = endPoint;
			IsClosed = isClosed;
		}
	}
}
