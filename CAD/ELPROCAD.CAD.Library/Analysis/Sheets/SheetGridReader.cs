using System;
using System.Collections.Generic;
using System.Linq;

#if ARX_APP
using AcDb = Autodesk.AutoCAD.DatabaseServices;
using AcGe = Autodesk.AutoCAD.Geometry;
#elif BRX_APP || PROGECAD_APP
using AcDb = Teigha.DatabaseServices;
using AcGe = Teigha.Geometry;
#endif

namespace ELPROCAD.CAD.Library.Analysis.Sheets
{
	/// <summary>
	/// Finds ruler markers without depending on container block names.
	/// </summary>
	internal sealed class SheetGridReader
	{
		internal SheetGrid? Read(AcDb.Transaction transaction, AcDb.ObjectId spaceId, string sheetLayer, double tolerance, out HashSet<AcDb.ObjectId> rulerRoots)
		{
			if (double.IsNaN(tolerance) ||
				double.IsInfinity(tolerance) ||
				tolerance <= 0)
			{
				throw new ArgumentOutOfRangeException(nameof(tolerance));
			}

			var roots = new HashSet<AcDb.ObjectId>();
			rulerRoots = roots;

			var markers =
				new Dictionary<string, (AcGe.Point3d Position, string[] Labels)>();

			var path = new HashSet<AcDb.ObjectId>();

			var space = (AcDb.BlockTableRecord)transaction.GetObject(
				spaceId, AcDb.OpenMode.ForRead);

			void Visit(
				AcDb.BlockReference block,
				AcGe.Matrix3d parentToWorld,
				AcDb.ObjectId rootId)
			{
				var contents = (AcDb.BlockTableRecord)transaction.GetObject(
					block.BlockTableRecord, AcDb.OpenMode.ForRead);

				// External references do not define this drawing's ruler.
				if (contents.IsFromExternalReference ||
					contents.IsFromOverlayReference)
				{
					return;
				}

				var named = block.IsDynamicBlock
					? (AcDb.BlockTableRecord)transaction.GetObject(
						block.DynamicBlockTableRecord,
						AcDb.OpenMode.ForRead)
					: contents;

				var name = named.Name.ToUpperInvariant();

				if (name == "EPCB_GRID_X" || name == "EPCB_GRID_Y")
				{
					try
					{
						var attributes = ReadAttributes(
							transaction, block, contents);

						attributes.TryGetValue("ROLE", out var roleText);

						var role = (roleText ?? string.Empty)
							.Trim()
							.ToUpperInvariant();

						if (role != "START" && role != "STEP")
							throw new ArgumentException(
								"ROLE must be START or STEP.");

						var key = name + ":" + role;

						if (markers.ContainsKey(key))
							throw new ArgumentException(
								$"Duplicate ruler marker '{key}'.");

						markers.Add(key, (
							block.Position.TransformBy(parentToWorld),
							role == "START"
								? SheetGridLabels.Read(attributes)
								: Array.Empty<string>()));

						roots.Add(rootId);
					}
					catch (ArgumentException exception)
					{
						throw new InvalidOperationException(
							$"Ruler root {rootId.Handle}, " +
							$"marker {block.Handle}: {exception.Message}",
							exception);
					}

					return;
				}

				if (!path.Add(contents.ObjectId))
					throw new InvalidOperationException(
						"The ruler search encountered recursive block nesting.");

				try
				{
					var toWorld = parentToWorld * block.BlockTransform;

					foreach (AcDb.ObjectId id in contents)
					{
						if (!id.IsValid || id.IsErased)
							continue;

						if (transaction.GetObject(
							id, AcDb.OpenMode.ForRead) is AcDb.BlockReference child)
						{
							Visit(child, toWorld, rootId);
						}
					}
				}
				finally
				{
					path.Remove(contents.ObjectId);
				}
			}

			// The top-level insertion selects the sheet.
			// Layers inside its block definition do not change that selection.
			foreach (AcDb.ObjectId id in space)
			{
				if (!id.IsValid || id.IsErased)
					continue;

				if (transaction.GetObject(
						id, AcDb.OpenMode.ForRead) is AcDb.BlockReference block &&
					string.Equals(
						block.Layer,
						sheetLayer,
						StringComparison.OrdinalIgnoreCase))
				{
					Visit(block, AcGe.Matrix3d.Identity, id);
				}
			}

			if (markers.Count == 0)
				return null;

			var required = new[]
			{
				"EPCB_GRID_X:START",
				"EPCB_GRID_X:STEP",
				"EPCB_GRID_Y:START",
				"EPCB_GRID_Y:STEP"
			};

			var missing = required
				.Where(key => !markers.ContainsKey(key))
				.ToArray();

			if (missing.Length != 0)
				throw new InvalidOperationException(
					"Incomplete ruler. Missing: " + string.Join(", ", missing));

			return new SheetGrid(
				markers[required[0]].Position,
				markers[required[1]].Position,
				markers[required[2]].Position,
				markers[required[3]].Position,
				markers[required[0]].Labels,
				markers[required[2]].Labels,
				tolerance);
		}

		private static Dictionary<string, string> ReadAttributes(AcDb.Transaction transaction, AcDb.BlockReference block, AcDb.BlockTableRecord contents)
		{
			var values =
				new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

			// Definition defaults also support constant attributes in nested blocks.
			foreach (AcDb.ObjectId id in contents)
			{
				if (!id.IsValid || id.IsErased)
					continue;

				if (transaction.GetObject(
					id, AcDb.OpenMode.ForRead) is AcDb.AttributeDefinition definition)
				{
					if (values.ContainsKey(definition.Tag))
						throw new ArgumentException(
							$"Duplicate attribute definition '{definition.Tag}'.");

					values.Add(definition.Tag, definition.TextString);
				}
			}

			var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			foreach (AcDb.ObjectId id in block.AttributeCollection)
			{
				if (!id.IsValid || id.IsErased)
					continue;

				if (transaction.GetObject(
					id, AcDb.OpenMode.ForRead) is AcDb.AttributeReference attribute)
				{
					if (!tags.Add(attribute.Tag))
						throw new ArgumentException(
							$"Duplicate attribute '{attribute.Tag}'.");

					values[attribute.Tag] = attribute.TextString;
				}
			}

			return values;
		}
	}
}