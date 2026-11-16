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

		/// <summary>
		/// Last analyzed insertion point in the document's world coordinate system.
		/// </summary>
		public double? PositionX { get; set; }
		public double? PositionY { get; set; }
		public double? PositionZ { get; set; }

		/// <summary>
		/// Unformatted ruler column label.
		/// </summary>
		[Size(255)]
		public string? GridColumn { get; set; }

		/// <summary>
		/// Unformatted ruler row label.
		/// </summary>
		[Size(255)]
		public string? GridRow { get; set; }

		[Indexed]
		public Guid EngineeringObjectId { get; set; }

		/// <summary>
		/// Gets or sets the sheet on which the CAD symbol is presented.
		/// </summary>
		[Indexed]
		public Guid SheetId { get; set; }

		public DateTime? DetachedAtUtc { get; set; }

		[NonPersistent]
		public bool IsDetached => DetachedAtUtc.HasValue;
	}
}
