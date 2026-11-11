namespace ELPROCAD.DataAccess.Contracts.MDBX.Documents
{
	/// <summary>
	/// Contains standard document summary properties.
	/// </summary>
	public interface IDocumentProperties : IEntity
	{
		Guid DocumentId { get; }

		string Title { get; set; }

		string Subject { get; set; }

		string Author { get; set; }

		string Keywords { get; set; }

		string Comments { get; set; }

		string LastSavedBy { get; set; }

		string RevisionNumber { get; set; }

		string HyperlinkBase { get; set; }

		int? LoadedVersion { get; }
	}
}
