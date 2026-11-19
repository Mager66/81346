using DevExpress.Xpo;

namespace ELPROCAD.DataAccess.Models.MDBX.Connections
{
	/// <summary>A local drawing net. Its number is unique only within its sheet.</summary>
	[Persistent("ConnectionNetworks")]
	[OptimisticLocking(true)]
	internal sealed class ConnectionNetwork : VersionedEntity
	{
		[Indexed(nameof(Number), Unique = true)]
		public Guid SheetId { get; set; }

		public int Number { get; set; }

		public bool IsFromAnalysis { get; set; }
	}
}
