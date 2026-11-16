namespace ELPROCAD.CAD.Library.Analysis.Sheets
{
	/// <summary>
	/// Resolves equal-sized intervals in ruler-local coordinates.
	/// </summary>
	internal sealed class SheetGridAxis
	{
		private readonly double _start;
		private readonly double _step;
		private readonly string[] _labels;

		// TODO: Delete, only for diagnostics
		internal int IntervalCount => _labels.Length;
		// TODO: Delete, only for diagnostics
		internal string LabelsText => string.Join("|", _labels);

		public SheetGridAxis(double start, double nextBoundary, IEnumerable<string> labels)
		{
			if (!IsFinite(start) || !IsFinite(nextBoundary))
				throw new ArgumentException("Marker coordinates must be finite.");

			_step = nextBoundary - start;

			if (!IsFinite(_step) || _step == 0)
				throw new ArgumentException("Markers must define a finite, non-zero interval.");

			if (labels == null)
				throw new ArgumentNullException(nameof(labels));

			_labels = labels.ToArray();

			if (_labels.Length == 0 || _labels.Any(string.IsNullOrWhiteSpace))			
				throw new ArgumentException("Every interval requires a label.", nameof(labels));

			if (_labels.Distinct(StringComparer.OrdinalIgnoreCase).Count() != _labels.Length)
				throw new ArgumentException("Axis labels must be unique.", nameof(labels));

			if (!IsFinite(start + _step * _labels.Length))
				throw new ArgumentException("The ruler end coordinate must be finite.");

			_start = start;
		}

		/// <summary>
		/// Internal boundaries belong to the following interval.
		/// The final outer boundary belongs to the last interval.
		/// Null indicates a position outside the ruler.
		/// </summary>
		public string? GetLabel(double position)
		{
			if (!IsFinite(position))
				throw new ArgumentOutOfRangeException(nameof(position), "The coordinate must be finite.");

			var index = (position - _start) / _step;

			if (!IsFinite(index) || index < 0 || index > _labels.Length)
				return null;

			return index == _labels.Length
				? _labels[_labels.Length - 1]
				: _labels[(int)Math.Floor(index)];
		}

		private static bool IsFinite(double value) =>
			!double.IsNaN(value) && !double.IsInfinity(value);
	}
}
