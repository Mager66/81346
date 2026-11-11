using DevExpress.Xpo;
using ELPROCAD.DataAccess.Contracts.MDBX.Documents;

namespace ELPROCAD.DataAccess.Models.MDBX.Documents
{
	[Persistent("CadDocumentCustomProperties")]
	[OptimisticLocking(true)]
	internal sealed class CadDocumentCustomProperty : VersionedEntity, ICadDocumentCustomProperty
	{
		[Indexed]
		public Guid DocumentId { get; set; }

		// A property name may occur once per document.
		[Size(255)]
		[Indexed(nameof(DocumentId), Unique = true)]
		public string Name { get; set; } = "";

		[Size(SizeAttribute.Unlimited)]
		public string Value { get; set; } = "";
	}
}
