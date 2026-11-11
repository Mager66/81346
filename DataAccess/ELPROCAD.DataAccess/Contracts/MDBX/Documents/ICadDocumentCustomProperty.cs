namespace ELPROCAD.DataAccess.Contracts.MDBX.Documents
{
	/// <summary>
	/// Contains a named custom property supplied by a document.
	/// </summary>
	public interface ICadDocumentCustomProperty : IEntity
	{
		Guid DocumentId { get; }

		string Name { get; set; }

		string Value { get; set; }

		int? LoadedVersion { get; }
	}
}
