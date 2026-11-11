using DevExpress.Xpo;
using ELPROCAD.DataAccess.Contracts.MDBX.Documents;

namespace ELPROCAD.DataAccess.Models.MDBX.Documents
{
	[Persistent("CadDocumentSpaces")]
	[OptimisticLocking(true)]
	internal sealed class CadDocumentSpace : VersionedEntity, ICadDocumentSpace
	{
		[Indexed]
		public Guid DocumentId { get; set; }

		[Size(255)]
		[Indexed(nameof(DocumentId), Unique = true)]
		public string Name { get; set; } = "";

		public CadSpaceKind SpaceKind { get; set; }
	}
}
