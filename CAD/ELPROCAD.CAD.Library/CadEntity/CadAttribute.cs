#if ARX_APP
using AcDb = Autodesk.AutoCAD.DatabaseServices;
#elif BRX_APP
using AcDb = Teigha.DatabaseServices;
#elif PROGECAD_APP
using AcDb = Teigha.DatabaseServices;
#endif

namespace ELPROCAD.CAD.Library.CadEntity
{

	/// <summary>
	/// Represents a snapshot of one CAD attribute reference.
	/// </summary>
	public sealed class CadAttribute
	{
		/// <summary>
		/// Gets the attribute reference identifier in the current database.
		/// </summary>
		public AcDb.ObjectId ObjectId { get; }

		/// <summary>
		/// Gets the owning block reference identifier.
		/// </summary>
		public AcDb.ObjectId OwnerId { get; }

		/// <summary>
		/// Gets the attribute tag.
		/// </summary>
		public string Tag { get; }

		/// <summary>
		/// Gets the attribute value captured during reading.
		/// </summary>
		public string Value { get; }

		/// <summary>
		/// Gets or sets the definition prompt.
		/// Null indicates that definition metadata has not been loaded.
		/// </summary>
		public string? Prompt { get; set; }

		/// <summary>
		/// Gets or sets the default value from the attribute definition.
		/// Null indicates that definition metadata has not been loaded.
		/// </summary>
		public string? Default { get; set; }

		/// <summary>
		/// Gets or sets the attribute text height captured during reading.
		/// </summary>
		public double Height { get; set; }

		/// <summary>
		/// Gets or sets whether the attribute was invisible when read.
		/// </summary>
		public bool IsInvisible { get; set; }

		public CadAttribute(AcDb.ObjectId objectId, AcDb.ObjectId ownerId, string tag, string value)
		{
			if (string.IsNullOrWhiteSpace(tag))
				throw new ArgumentException(
					"An attribute tag is required.",
					nameof(tag));

			ObjectId = objectId;
			OwnerId = ownerId;
			Tag = tag;
			Value = value ?? throw new ArgumentNullException(nameof(value));
		}
	}
}
