namespace ELPROCAD.DataAccess.Contracts.PDSX.Documents
{
	/// <summary>
	/// Describes a program used to open documents.
	/// </summary>
	public interface IOpeningProgram : IEntity
	{
		string Name { get; set; }

		string ExecutablePath { get; set; }

		int? LoadedVersion { get; }
	}
}
