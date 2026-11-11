namespace ELPROCAD.DataAccess.Contracts.MDBX.Documents
{
	/// <summary>
	/// Identifies a space within a CAD document.
	/// </summary>
	public interface ICadDocumentSpace : IEntity
	{
		Guid DocumentId { get; }

		string Name { get; set; }

		CadSpaceKind SpaceKind { get; }

		int? LoadedVersion { get; }
	}
}
