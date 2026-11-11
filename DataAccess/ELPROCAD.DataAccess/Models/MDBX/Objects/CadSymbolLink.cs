using DevExpress.Xpo;
using ELPROCAD.DataAccess.Contracts.MDBX.Objects;

namespace ELPROCAD.DataAccess.Models.MDBX.Objects
{
	[Persistent("CadSymbolLinks")]
	[OptimisticLocking(true)]
	internal sealed class CadSymbolLink : VersionedEntity, ICadSymbolLink
	{
		[Indexed]
		public Guid DocumentId { get; set; }

		/// <summary>
		/// Identifies the block reference within its document.
		/// </summary>
		[Indexed(nameof(DocumentId), Unique = true)]
		public long BlockHandle { get; set; }

		[Indexed]
		public Guid EngineeringObjectId { get; set; }
	}
}
