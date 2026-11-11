namespace ELPROCAD.DataAccess.Contracts.PDSX.Documents
{
	/// <summary>
	/// Provides a snapshot of the configuration used to open a file format.
	/// </summary>
	public interface IOpeningConfiguration
	{
		Guid FileFormatId { get; }

		Guid AssociationId { get; }

		Guid OpeningProgramId { get; }

		string ProgramName { get; }

		string ExecutablePath { get; }

		string ArgumentsTemplate { get; }
	}
}
