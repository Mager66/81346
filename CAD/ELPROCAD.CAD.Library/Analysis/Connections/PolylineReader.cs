#if BRX_APP
using AcDb = Teigha.DatabaseServices;
using AcGe = Teigha.Geometry;
#endif
namespace ELPROCAD.CAD.Library.Analysis.Connections
{
	public sealed class PolylineReader
	{
		private readonly double tolerance;

		public PolylineReader(double tolerance)
		{
			if (double.IsNaN(tolerance) ||
				double.IsInfinity(tolerance) ||
				tolerance <= 0)
			{
				throw new ArgumentOutOfRangeException(
					nameof(tolerance),
					"Toleransen måste vara ett positivt, ändligt tal.");
			}

			this.tolerance = tolerance;
		}

		public PolylineAnalysisData? Read(AcDb.Transaction transaction, AcDb.ObjectId polylineId)
		{
			if (transaction == null)
				throw new ArgumentNullException(nameof(transaction));

			var entity = transaction.GetObject(
				polylineId,
				AcDb.OpenMode.ForRead);

			if (entity is not AcDb.Polyline &&
				entity is not AcDb.Polyline2d &&
				entity is not AcDb.Polyline3d)
			{
				throw new ArgumentException(
					"Objektet måste vara en polyline.",
					nameof(polylineId));
			}

			var curve = (AcDb.Curve)entity;

			if (!curve.Closed &&
				!IsOrthogonal(transaction, curve))
			{
				return null;
			}

			return new PolylineAnalysisData(
				polylineId,
				curve.StartPoint,
				curve.EndPoint,
				curve.Closed);
		}

		private bool IsOrthogonal(AcDb.Transaction transaction,	AcDb.Curve curve)
		{
			var points = new List<AcGe.Point3d>();

			switch (curve)
			{
				case AcDb.Polyline polyline:
					for (var index = 0;
						index < polyline.NumberOfVertices;
						index++)
					{
						if (index < polyline.NumberOfVertices - 1 &&
							polyline.GetBulgeAt(index) != 0.0)
						{
							return false;
						}

						points.Add(polyline.GetPoint3dAt(index));
					}
					break;

				case AcDb.Polyline2d polyline:
					if (polyline.PolyType != AcDb.Poly2dType.SimplePoly)
						return false;

					var bulges = new List<double>();

					foreach (AcDb.ObjectId vertexId in polyline)
					{
						var vertex = (AcDb.Vertex2d)transaction.GetObject(
							vertexId,
							AcDb.OpenMode.ForRead);

						bulges.Add(vertex.Bulge);
					}

					for (var index = 0; index < bulges.Count; index++)
					{
						if (index < bulges.Count - 1 &&
							bulges[index] != 0.0)
						{
							return false;
						}

						points.Add(curve.GetPointAtParameter(
							curve.StartParam + index));
					}
					break;

				case AcDb.Polyline3d polyline:
					if (polyline.PolyType != AcDb.Poly3dType.SimplePoly)
						return false;

					foreach (AcDb.ObjectId vertexId in polyline)
					{
						var vertex = (AcDb.PolylineVertex3d)
							transaction.GetObject(
								vertexId,
								AcDb.OpenMode.ForRead);

						points.Add(vertex.Position);
					}
					break;
			}

			if (points.Count < 2)
				return false;

			for (var index = 1; index < points.Count; index++)
			{
				var difference = points[index] - points[index - 1];

				var horizontal =
					Math.Abs(difference.X) > tolerance &&
					Math.Abs(difference.Y) <= tolerance;

				var vertical =
					Math.Abs(difference.Y) > tolerance &&
					Math.Abs(difference.X) <= tolerance;

				if (Math.Abs(difference.Z) > tolerance ||
					(!horizontal && !vertical))
				{
					return false;
				}
			}
			return true;
		}
	}
}
