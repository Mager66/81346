using ELPROCAD.CAD.Library.Analysis.Terminals;

namespace ELPROCAD.CAD.Library.Analysis.Connections
{
	public sealed class TerminalEndpointMatcher
	{
		private readonly double tolerance;

		public TerminalEndpointMatcher(double tolerance)
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

		public IReadOnlyList<TerminalEndpointMatch> Match(PolylineAnalysisData polyline, IEnumerable<BlockTerminal> terminals)
		{
			if (polyline == null)
				throw new ArgumentNullException(nameof(polyline));

			if (terminals == null)
				throw new ArgumentNullException(nameof(terminals));

			var matches = new List<TerminalEndpointMatch>();

			if (polyline.IsClosed)
				return matches.AsReadOnly();

			foreach (var terminal in terminals)
			{
				if (polyline.StartPoint.DistanceTo(terminal.Position)
					<= tolerance)
				{
					matches.Add(new TerminalEndpointMatch(
						polyline.ObjectId,
						true,
						terminal));
				}

				if (polyline.EndPoint.DistanceTo(terminal.Position)
					<= tolerance)
				{
					matches.Add(new TerminalEndpointMatch(
						polyline.ObjectId,
						false,
						terminal));
				}
			}

			return matches.AsReadOnly();
		}
		
	}
}
