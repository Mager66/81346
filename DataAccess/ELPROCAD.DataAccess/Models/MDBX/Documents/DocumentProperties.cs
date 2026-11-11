using DevExpress.Xpo;
using ELPROCAD.DataAccess.Contracts.MDBX.Documents;

namespace ELPROCAD.DataAccess.Models.MDBX.Documents
{
	[Persistent("DocumentProperties")]
	[OptimisticLocking(true)]
	internal sealed class DocumentProperties : VersionedEntity, IDocumentProperties
	{
		// Each document has at most one summary-properties record.
		[Indexed(Unique = true)]
		public Guid DocumentId { get; set; }

		[Size(SizeAttribute.Unlimited)]
		public string Title { get; set; } = "";

		[Size(SizeAttribute.Unlimited)]
		public string Subject { get; set; } = "";

		[Size(SizeAttribute.Unlimited)]
		public string Author { get; set; } = "";

		[Size(SizeAttribute.Unlimited)]
		public string Keywords { get; set; } = "";

		[Size(SizeAttribute.Unlimited)]
		public string Comments { get; set; } = "";

		[Size(SizeAttribute.Unlimited)]
		public string LastSavedBy { get; set; } = "";

		[Size(SizeAttribute.Unlimited)]
		public string RevisionNumber { get; set; } = "";

		[Size(SizeAttribute.Unlimited)]
		public string HyperlinkBase { get; set; } = "";
	}
}
