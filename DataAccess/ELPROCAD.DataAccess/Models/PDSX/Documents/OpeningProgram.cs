using DevExpress.Xpo;
using ELPROCAD.DataAccess.Contracts.PDSX.Documents;

namespace ELPROCAD.DataAccess.Models.PDSX.Documents
{
	[Persistent("OpeningPrograms")]
	[OptimisticLocking(true)]
	internal sealed class OpeningProgram : VersionedEntity, IOpeningProgram
	{
		[Size(50)]
		public string Name { get; set; } = "";

		[Size(1024)]
		public string ExecutablePath { get; set; } = "";
	}
}
