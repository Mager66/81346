using DevExpress.Xpo;
using ELPROCAD.DataAccess.Contracts.PDSX.Documents;

namespace ELPROCAD.DataAccess.Models.PDSX.Documents
{
	[Persistent("FileFormats")]
	[OptimisticLocking(true)]
	internal sealed class FileFormat : VersionedEntity, IFileFormat
	{
		[Size(50)]
		public string Name { get; set; } = "";

		[Size(16)]
		[Indexed(Unique = true)]
		public string Extension { get; set; } = "";

		[Size(255)]
		public string Description { get; set; } = "";

		public Guid? DefaultProgramAssociationId { get; set; }

		/// <summary>
		/// Forces association changes to participate in optimistic concurrency
		/// on the parent format.
		/// </summary>
		public long ProgramAssociationsRevision { get; set; }
	}
}
