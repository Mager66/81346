namespace ELPROCAD.DataAccess.Contracts.MDBX.References
{
	/// <summary>
	/// Stores reference-contact attributes without occupying object terminals.
	/// </summary>
	public interface ICadReferenceContact : IEntity
	{
		Guid CadSymbolLinkId { get; }
		string BlockName { get; }
		string Designation { get; }
		string Placement { get; }
		string ReferenceText { get; }
		int? LoadedVersion { get; }
	}
}
