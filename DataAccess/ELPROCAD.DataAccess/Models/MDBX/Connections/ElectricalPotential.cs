using DevExpress.Xpo;

namespace ELPROCAD.DataAccess.Models.MDBX.Connections
{
	/// <summary>
	/// A project potential such as PE, with no terminal conductor limit.
	/// Its GUID establishes identity; equal names alone do not merge potentials.
	/// </summary>
	[Persistent("ElectricalPotentials")]
	[OptimisticLocking(true)]
	internal sealed class ElectricalPotential : VersionedEntity
	{
		[Indexed]
		public Guid ProjectId { get; set; }

		[Size(255)]
		public string Name { get; set; } = string.Empty;
	}
}
