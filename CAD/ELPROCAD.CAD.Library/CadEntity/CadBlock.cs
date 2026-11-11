
#if ARX_APP
using AcDb = Autodesk.AutoCAD.DatabaseServices;
using AcGe = Autodesk.AutoCAD.Geometry;
#elif BRX_APP || PROGECAD_APP
using ELPROCAD.CAD.Library.Analysis.Terminals;
using AcDb = Teigha.DatabaseServices;
using AcGe = Teigha.Geometry;
#endif

namespace ELPROCAD.CAD.Library.CadEntity
{
	/// <summary>
	/// Represents a snapshot of one CAD block reference.
	/// </summary>
	public sealed class CadBlock
	{
		/// <summary>
		/// Gets the block reference identifier in the current database.
		/// </summary>
		public AcDb.ObjectId ObjectId { get; }

		/// <summary>
		/// Gets the block name captured during reading.
		/// </summary>
		public string BlockName { get; }

		/// <summary>
		/// Gets the insertion point captured during reading.
		/// </summary>
		public AcGe.Point3d Position { get; }

		/// <summary>
		/// Gets the attributes captured for this block reference.
		/// </summary>
		public IReadOnlyList<CadAttribute>? Attributes { get; }

		/// <summary>
		/// Finds an attribute by tag using a case-insensitive comparison.
		/// Returns null when the tag is not present.
		/// </summary>
		public CadAttribute? GetAttribute(string tag)
		{
			if (string.IsNullOrWhiteSpace(tag))
				throw new ArgumentException("An attribute tag is required.", nameof(tag));

			return Attributes.FirstOrDefault(attribute =>
				string.Equals(
					attribute.Tag,
					tag,
					StringComparison.OrdinalIgnoreCase));
		}

		/// <summary>
		/// Gets the terminals resolved during block analysis.
		/// </summary>
		public IReadOnlyList<BlockTerminal>? Terminals { get; }

		public int? ObjectType { get; }

		public bool HasDefinition => ObjectType.HasValue;

		public CadBlock(
			AcDb.ObjectId objectId,	
			string blockName, 
			AcGe.Point3d position, 
			IEnumerable<CadAttribute>? attributes,
			int? objectType,
			IEnumerable<BlockTerminal>? terminals)
		{
			if (string.IsNullOrWhiteSpace(blockName))
				throw new ArgumentException("A block name is required.", nameof(blockName));

			if (attributes == null)
				throw new ArgumentNullException(nameof(attributes));

			var attributeItems = attributes.ToList();

			if (attributeItems.Any(attribute => attribute == null))
				throw new ArgumentException("The attribute collection must not contain null entries.", nameof(attributes));

			ObjectId = objectId;
			BlockName = blockName;
			ObjectType = objectType;
			Position = position;
			Attributes = attributeItems == null
				? null
				: attributeItems.AsReadOnly();
			Terminals = terminals == null
				? null
				: new List<BlockTerminal>(terminals).AsReadOnly();
		}
	}
}
