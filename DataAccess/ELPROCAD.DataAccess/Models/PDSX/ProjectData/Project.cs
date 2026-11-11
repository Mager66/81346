using DevExpress.Xpo;
using ELPROCAD.DataAccess.Contracts.PDSX.ProjectData;

namespace ELPROCAD.DataAccess.Models.PDSX.ProjectData
{
	/// <summary>
	/// Stores project metadata independently of a database session.
	/// </summary>
	[Persistent("Project")]
	[OptimisticLocking(true)]
	internal sealed class Project : VersionedEntity, IProject
	{
		[Size(50)]
		public string Name { get; set; } = "";

		[Size(255)]
		public string Path { get; set; } = "";

		[Size(255)]
		public string Description { get; set; } = "";
	}
}
