using DevExpress.Xpo;
using ELPROCAD.DataAccess.Contracts.MDBX.Documents;

namespace ELPROCAD.DataAccess.Models.MDBX.Documents
{
	[Persistent("Documents")]
	[OptimisticLocking(true)]
	internal sealed class Document : VersionedEntity, IDocument
	{
		// References the project identity without an XPO cross-database association.
		[Indexed]
		public Guid ProjectId { get; set; }

		[Size(50)]
		public string Name { get; set; } = "";

		[Size(255)]
		public string Path { get; set; } = "";

		public Guid FileFormatId {  get; set; }
	}
}
