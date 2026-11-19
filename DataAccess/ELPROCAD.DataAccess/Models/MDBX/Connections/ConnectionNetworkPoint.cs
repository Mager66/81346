using DevExpress.Xpo;

namespace ELPROCAD.DataAccess.Models.MDBX.Connections
{
	/// <summary>An unconnected point has no membership. One point belongs to at most one local net.</summary>
	[Persistent("ConnectionNetworkPoints")]
	[OptimisticLocking(true)]
	internal sealed class ConnectionNetworkPoint : VersionedEntity
	{
		// TODO: Store physical conductors and user-controlled wiring order separately.
		// Membership expresses connectivity, not the number or routing order of wires.
		[Indexed]
		public Guid NetworkId { get; set; }

		[Indexed(Unique = true)]
		public Guid PointId { get; set; }
	}
}
