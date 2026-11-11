using DevExpress.Xpo;
using ELPROCAD.DataAccess.Contracts.MDBX.Aspects;

namespace ELPROCAD.DataAccess.Models.MDBX.Aspects
{
	/// <summary>
	/// Stores shared node data without defining the aspect's meaning or notation.
	/// </summary>
	[NonPersistent]
	internal abstract class AspectNode : VersionedEntity, IAspectNode
	{
		[Indexed]
		public Guid ProjectId { get; set; }

		// A designation must be unique at its depth within the project's aspect.
		[Size(255)]
		[Indexed(nameof(ProjectId) + ";" + nameof(Depth), Unique = true)]
		public string Designation { get; set; } = "";

		// Refers to a node in the same concrete aspect table.
		[Indexed]
		public Guid? ParentId { get; set; }

		// Identifies an object representing this node, when applicable.
		[Indexed]
		public Guid? RepresentedByObjectId { get; set; }

		public int Depth { get; set; }
	}
}
