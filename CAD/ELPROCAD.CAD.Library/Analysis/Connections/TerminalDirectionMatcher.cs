#if BRX_APP
using AcDb = Teigha.DatabaseServices;
#endif

namespace ELPROCAD.CAD.Library.Analysis.Connections
{
	public sealed class TerminalDirectionMatcher
	{
		private readonly double minimumDotProduct;

		public TerminalDirectionMatcher(double angularToleranceRadians)
		{
			if (double.IsNaN(angularToleranceRadians) ||
				double.IsInfinity(angularToleranceRadians) ||
				angularToleranceRadians <= 0 ||
				angularToleranceRadians >= Math.PI / 2)
			{
				throw new ArgumentOutOfRangeException(
					nameof(angularToleranceRadians),
					"Vinkeltoleransen måste vara större än 0 och mindre än π/2.");
			}

			minimumDotProduct = Math.Cos(angularToleranceRadians);
		}

		public bool Matches(AcDb.Transaction transaction, TerminalEndpointMatch endpointMatch)
		{
			if (transaction == null)
				throw new ArgumentNullException(nameof(transaction));

			if (endpointMatch == null)
				throw new ArgumentNullException(nameof(endpointMatch));

			if (transaction.GetObject(
				endpointMatch.PolylineId,
				AcDb.OpenMode.ForRead) is not AcDb.Curve curve)
			{
				throw new ArgumentException(
					"Träffen måste hänvisa till en kurva.",
					nameof(endpointMatch));
			}

			if (curve.Closed)
				return false;

			var parameter = endpointMatch.IsStartPoint
				? curve.StartParam
				: curve.EndParam;

			var outgoingDirection = curve.GetFirstDerivative(parameter);

			if (!endpointMatch.IsStartPoint)
				outgoingDirection = outgoingDirection.MultiplyBy(-1.0);

			var terminalDirection =
				endpointMatch.Terminal.ConnectionDirection;

			if (outgoingDirection.Length == 0 ||
				terminalDirection.Length == 0)
			{
				return false;
			}

			var dotProduct = outgoingDirection
				.GetNormal()
				.DotProduct(terminalDirection.GetNormal());

			return dotProduct >= minimumDotProduct;
		}
	}
}
