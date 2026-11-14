namespace ELPROCAD.DataAccess.Contracts.MDBX.References
{
	/// <summary>
	/// Stores a reference attribute value, not a physical terminal assignment.
	/// </summary>
	public interface ICadReferenceContactTerminal : IEntity
	{
		Guid ReferenceContactId { get; }
		string Tag { get; }
		string Value { get; }
		int? LoadedVersion { get; }
	}
}
