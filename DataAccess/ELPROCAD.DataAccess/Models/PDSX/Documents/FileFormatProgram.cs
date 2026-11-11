using DevExpress.Xpo;
using ELPROCAD.DataAccess.Contracts.PDSX.Documents;

namespace ELPROCAD.DataAccess.Models.PDSX.Documents
{
	[Persistent("FileFormatPrograms")]
	[OptimisticLocking(true)]
	internal sealed class FileFormatProgram	: VersionedEntity, IFileFormatProgram
	{
		// Each format/program combination may occur only once.
		[Indexed(nameof(OpeningProgramId), Unique = true)]
		public Guid FileFormatId { get; set; }

		[Indexed]
		public Guid OpeningProgramId { get; set; }

		[Size(2048)]
		public string Arguments { get; set; } = "";
	}
}
