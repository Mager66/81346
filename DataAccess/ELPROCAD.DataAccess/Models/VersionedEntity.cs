using DevExpress.Xpo;

namespace ELPROCAD.DataAccess.Models
{
	/// <summary>
	/// Stores the version captured when an entity was loaded or saved.
	/// </summary>
	[NonPersistent]
	internal abstract class VersionedEntity : PocoEntity
	{
		[NonPersistent]
		public int? LoadedVersion { get; internal set; }

		/// <summary>
		/// Coordinates dependency changes with concurrent updates or deletion.
		/// This property is persisted but not exposed through public contracts.
		/// </summary>
		public long DependencyRevision { get; set; }
	}
}
