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
	}
}