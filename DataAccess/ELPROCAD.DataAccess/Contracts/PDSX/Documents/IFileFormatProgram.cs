namespace ELPROCAD.DataAccess.Contracts.PDSX.Documents
{
	/// <summary>
	/// Associates a file format with an opening program and its launch arguments.
	/// </summary>
	public interface IFileFormatProgram : IEntity
	{
		Guid FileFormatId { get; }

		Guid OpeningProgramId { get; }

		string Arguments { get; set; }

		int? LoadedVersion { get; }
	}
}
