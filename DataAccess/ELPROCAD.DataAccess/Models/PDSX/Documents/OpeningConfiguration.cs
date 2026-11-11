using ELPROCAD.DataAccess.Contracts.PDSX.Documents;

namespace ELPROCAD.DataAccess.Models.PDSX.Documents
{
	internal sealed class OpeningConfiguration : IOpeningConfiguration
	{
		internal OpeningConfiguration(
			Guid fileFormatId,
			Guid associationId,
			Guid openingProgramId,
			string programName,
			string executablePath,
			string argumentsTemplate)
		{
			FileFormatId = fileFormatId;
			AssociationId = associationId;
			OpeningProgramId = openingProgramId;
			ProgramName = programName;
			ExecutablePath = executablePath;
			ArgumentsTemplate = argumentsTemplate;
		}

		public Guid FileFormatId { get; }

		public Guid AssociationId { get; }

		public Guid OpeningProgramId { get; }

		public string ProgramName { get; }

		public string ExecutablePath { get; }

		public string ArgumentsTemplate { get; }
	}
}
