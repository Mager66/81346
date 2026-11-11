using DevExpress.Xpo;
using ELPROCAD.DataAccess.Contracts.MDBX.Documents;

namespace ELPROCAD.DataAccess.Models.MDBX.Documents
{
	[Persistent("Sheets")]
	[OptimisticLocking(true)]
	internal sealed class Sheet : VersionedEntity, ISheet
	{
		[Indexed]
		public Guid DocumentId { get; set; }

		// Sheet numbers must be unique within their document.
		[Size(10)]
		[Indexed(nameof(DocumentId), Unique = true)]
		public string SheetNo { get; set; } = "";
	}
}
