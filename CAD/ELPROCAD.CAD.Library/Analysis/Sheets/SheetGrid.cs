using System;
using System.Collections.Generic;

#if ARX_APP
using AcGe = Autodesk.AutoCAD.Geometry;
#elif BRX_APP || PROGECAD_APP
using AcGe = Teigha.Geometry;
#endif

namespace ELPROCAD.CAD.Library.Analysis.Sheets
{
	/// <summary>
	/// Resolves world positions against two transformed ruler axes.
	/// </summary>
	internal sealed class SheetGrid
	{
		private readonly AcGe.Point3d _xStart, _yStart;
		private readonly AcGe.Vector3d _x, _y, _normal;

		private readonly double _xLength, _yLength;
		private readonly double _dot, _denominator, _tolerance;

		private readonly SheetGridAxis _columns, _rows;

		internal SheetGrid(
			AcGe.Point3d xStart,
			AcGe.Point3d xNext,
			AcGe.Point3d yStart,
			AcGe.Point3d yNext,
			IEnumerable<string> columns,
			IEnumerable<string> rows,
			double tolerance)
		{
			_xStart = xStart;
			_yStart = yStart;
			_tolerance = tolerance;

			var dx = xNext - xStart;
			var dy = yNext - yStart;

			_xLength = dx.Length;
			_yLength = dy.Length;

			if (!Finite(_xLength) || !Finite(_yLength) ||
				_xLength <= tolerance || _yLength <= tolerance)
			{
				throw new InvalidOperationException(
					"Ruler markers are too close or invalid.");
			}

			_x = dx / _xLength;
			_y = dy / _yLength;

			var cross = _x.CrossProduct(_y);
			_denominator = cross.LengthSqrd;

			if (_denominator < 1e-12)
				throw new InvalidOperationException(
					"Ruler axes must not be parallel.");

			_normal = cross / cross.Length;
			_dot = _x.DotProduct(_y);

			if (Math.Abs((yStart - xStart).DotProduct(_normal)) > tolerance)
				throw new InvalidOperationException(
					"Ruler markers must share a plane.");

			_columns = new SheetGridAxis(0, 1, columns);
			_rows = new SheetGridAxis(0, 1, rows);
		}

		internal (string X, string Y)? GetCoordinate(AcGe.Point3d position)
		{
			if (!Finite(position.X) ||
				!Finite(position.Y) ||
				!Finite(position.Z))
			{
				throw new ArgumentException(
					"The position must be finite.",
					nameof(position));
			}

			var vx = position - _xStart;

			if (Math.Abs(vx.DotProduct(_normal)) > _tolerance)
				return null;

			var vy = position - _yStart;

			var x = (vx.DotProduct(_x) - _dot * vx.DotProduct(_y)) /
				(_denominator * _xLength);

			var y = (vy.DotProduct(_y) - _dot * vy.DotProduct(_x)) /
				(_denominator * _yLength);

			// Stabilize boundary decisions after nested CAD transformations.
			x = Snap(x, _tolerance / (_xLength * Math.Sqrt(_denominator)));
			y = Snap(y, _tolerance / (_yLength * Math.Sqrt(_denominator)));

			var column = _columns.GetLabel(x);
			var row = _rows.GetLabel(y);

			return column == null || row == null
				? null
				: (column, row);
		}

		private static double Snap(double value, double tolerance) =>
			Math.Abs(value - Math.Round(value)) <= tolerance
				? Math.Round(value)
				: value;

		private static bool Finite(double value) =>
			!double.IsNaN(value) && !double.IsInfinity(value);

		// TODO: Delete, only for diagnostics
		internal IReadOnlyList<string> MarkerDiagnostics { get; set; } =
	Array.Empty<string>();

		internal IReadOnlyList<string> Describe()
		{
			var lines = new List<string>(MarkerDiagnostics);

			lines.Add(FormattableString.Invariant(
				$"GRID: tolerance={_tolerance:G17}; axisDot={_dot:G17}; denominator={_denominator:G17}"));

			lines.Add(FormattableString.Invariant(
				$"X: stepLength={_xLength:G17}; count={_columns.IntervalCount}; span={_xLength * _columns.IntervalCount:G17}; labels=[{_columns.LabelsText}]"));

			lines.Add(FormattableString.Invariant(
				$"Y: stepLength={_yLength:G17}; count={_rows.IntervalCount}; span={_yLength * _rows.IntervalCount:G17}; labels=[{_rows.LabelsText}]"));

			return lines.AsReadOnly();
		}

		/// <summary>
		/// Reports the same projection and boundary tests used by GetCoordinate.
		/// </summary>
		internal string DescribePosition(AcGe.Point3d position)
		{
			var vx = position - _xStart;
			var vy = position - _yStart;
			var planeDistance = Math.Abs(vx.DotProduct(_normal));

			var rawX = (vx.DotProduct(_x) - _dot * vx.DotProduct(_y)) /
				(_denominator * _xLength);

			var rawY = (vy.DotProduct(_y) - _dot * vy.DotProduct(_x)) /
				(_denominator * _yLength);

			var x = Snap(
				rawX,
				_tolerance / (_xLength * Math.Sqrt(_denominator)));

			var y = Snap(
				rawY,
				_tolerance / (_yLength * Math.Sqrt(_denominator)));

			var column = Finite(x) ? _columns.GetLabel(x) : null;
			var row = Finite(y) ? _rows.GetLabel(y) : null;

			var reasons = new List<string>();

			if (!Finite(position.X) ||
				!Finite(position.Y) ||
				!Finite(position.Z))
			{
				reasons.Add("INVALID_POSITION");
			}

			if (!Finite(planeDistance) || planeDistance > _tolerance)
				reasons.Add("OFF_PLANE");

			if (column == null)
				reasons.Add("OUTSIDE_COLUMNS");

			if (row == null)
				reasons.Add("OUTSIDE_ROWS");

			return FormattableString.Invariant(
					$"WCS={PointText(position)}; planeDistance={planeDistance:G17}; ") +
				FormattableString.Invariant(
					$"X(raw={rawX:G17}, snapped={x:G17}, allowed=0..{_columns.IntervalCount}, label='{column ?? "<none>"}'); ") +
				FormattableString.Invariant(
					$"Y(raw={rawY:G17}, snapped={y:G17}, allowed=0..{_rows.IntervalCount}, label='{row ?? "<none>"}'); ") +
				$"result={(reasons.Count == 0 ? "OK" : string.Join(",", reasons))}";
		}

		internal static string PointText(AcGe.Point3d point) =>
			FormattableString.Invariant(
				$"({point.X:G17},{point.Y:G17},{point.Z:G17})");
	}
}