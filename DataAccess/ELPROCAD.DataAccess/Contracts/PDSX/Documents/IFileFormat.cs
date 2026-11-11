namespace ELPROCAD.DataAccess.Contracts.PDSX.Documents
{
	/// <summary>
	/// Describes a document kind stored in the PDSX database.
	/// </summary>
	public interface IFileFormat : IEntity
	{
		string Name { get; set; }

		string Extension { get; set; }

		string Description { get; set; }

		int? LoadedVersion { get; }

		/// <summary>
		/// Identifies the default format/program association, if one is selected.
		/// </summary>
		Guid? DefaultProgramAssociationId { get; }
	}
}
