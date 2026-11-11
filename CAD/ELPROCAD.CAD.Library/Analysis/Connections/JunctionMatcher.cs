#if BRX_APP
using AcDb = Teigha.DatabaseServices;
#endif

namespace ELPROCAD.CAD.Library.Analysis.Connections
{
	public sealed class JunctionMatcher
	{
		private readonly double tolerance;

		public JunctionMatcher(double tolerance)
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

		public IReadOnlyList<JunctionPolylineMatch> Match(AcDb.Transaction transaction, ConnectionJunction junction, IEnumerable<PolylineAnalysisData> polylines)
		{
			if (transaction == null)
				throw new ArgumentNullException(nameof(transaction));

			if (junction == null)
				throw new ArgumentNullException(nameof(junction));

			if (polylines == null)
				throw new ArgumentNullException(nameof(polylines));

			var matches = new List<JunctionPolylineMatch>();
			var visited = new HashSet<AcDb.ObjectId>();

			foreach (var polyline in polylines)
			{
				if (polyline.IsClosed || !visited.Add(polyline.ObjectId))
					continue;

				if (transaction.GetObject(
					polyline.ObjectId,
					AcDb.OpenMode.ForRead) is not AcDb.Curve curve)
				{
					throw new InvalidOperationException(
						"Polyline-posten måste hänvisa till en kurva.");
				}

				if (curve.Closed)
					continue;

				var atStartPoint = curve.StartPoint
					.DistanceTo(junction.Position) <= tolerance;

				var atEndPoint = curve.EndPoint
					.DistanceTo(junction.Position) <= tolerance;

				if (!atStartPoint && !atEndPoint)
				{
					var closestPoint = curve.GetClosestPointTo(
						junction.Position,
						false);

					if (closestPoint.DistanceTo(junction.Position) > tolerance)
						continue;
				}
				matches.Add(new JunctionPolylineMatch(
					junction,
					polyline.ObjectId,
					atStartPoint,
					atEndPoint));
			}
			return matches.AsReadOnly();
		}
	}
}
