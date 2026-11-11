namespace ELPROCAD.DataAccess.Contracts.MDBX.Aspects
{
	/// <summary>
	/// Describes a node in a configurable recursive aspect structure.
	/// </summary>
	public interface IAspectNode : IEntity
	{
		Guid ProjectId { get; }

		string Designation { get; set; }

		Guid? ParentId { get; set; }

		Guid? RepresentedByObjectId { get; set; }

		int? LoadedVersion { get; }

		/// <summary>
		/// Gets the node's depth. Root nodes have depth zero.
		/// </summary>
		int Depth { get; }
	}
}
