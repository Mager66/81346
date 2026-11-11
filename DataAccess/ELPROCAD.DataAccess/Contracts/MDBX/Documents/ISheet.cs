namespace ELPROCAD.DataAccess.Contracts.MDBX.Documents
{
	/// <summary>
	/// Describes a sheet belonging to a document.
	/// </summary>
	public interface ISheet : IEntity
	{
		Guid DocumentId { get; }

		string SheetNo { get; set; }

		int? LoadedVersion { get; }
	}
}
