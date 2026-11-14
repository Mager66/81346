using DevExpress.Xpo;
using ELPROCAD.DataAccess.Contracts.MDBX.Objects;

namespace ELPROCAD.DataAccess.Models.MDBX.Terminals
{
	[Persistent("EngineeringObjectTerminals")]
	[OptimisticLocking(true)]
	internal sealed class EngineeringObjectTerminal : VersionedEntity, IEngineeringObjectTerminal
	{
		[Indexed]
		public Guid EngineeringObjectId { get; set; }

		/// <summary>The actual terminal designation, for example A1, rather than UTTAG1.</summary>
		[Size(255)]
		[Indexed(nameof(EngineeringObjectId), Unique = true)]
		public string Designation { get; set; } = string.Empty;
	}
}
