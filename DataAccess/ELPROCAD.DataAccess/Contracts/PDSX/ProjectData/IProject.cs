namespace ELPROCAD.DataAccess.Contracts.PDSX.ProjectData
{
	/// <summary>
	/// Describes a project stored in a PDSX database.
	/// </summary>
	public interface IProject : IEntity
	{
		string Name { get; set; }

		string Path { get; set; }

		string Description { get; set; }

		/// <summary>
		/// Gets the version captured when this project was loaded or saved.
		/// </summary>
		int? LoadedVersion { get; }
	}
}
