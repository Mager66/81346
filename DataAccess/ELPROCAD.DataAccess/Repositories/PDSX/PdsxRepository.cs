using ELPROCAD.DataAccess.Models.PDSX.Documents;
using ELPROCAD.DataAccess.Repositories.PDSX.Classification;
using ELPROCAD.DataAccess.Repositories.PDSX.Documents;
using ELPROCAD.DataAccess.Repositories.PDSX.ProjectData;

namespace ELPROCAD.DataAccess.Repositories.PDSX
{
	internal sealed class PdsxRepository : IPdsxRepository
	{
		public IMainClassRepository MainClass { get; }

		public ISubClassRepository SubClass { get; }

		public IProjectRepository Project { get; }

		public IFileFormatRepository FileFormat { get; }

		public IOpeningProgramRepository OpeningProgram { get; }

		public IFileFormatProgramRepository FileFormatProgram { get; }

		internal PdsxRepository(IMainClassRepository mainClass, ISubClassRepository subClass, IProjectRepository project, IFileFormatRepository fileFormat, IOpeningProgramRepository openingProgram, IFileFormatProgramRepository fileFormatProgram)
		{
			MainClass = mainClass ?? throw new ArgumentNullException(nameof(mainClass));
			SubClass = subClass ?? throw new ArgumentNullException(nameof(subClass));
			Project = project ?? throw new ArgumentNullException(nameof(project));
			FileFormat = fileFormat ?? throw new ArgumentNullException(nameof(fileFormat));
			OpeningProgram = openingProgram ?? throw new ArgumentNullException(nameof(openingProgram));
			FileFormatProgram = fileFormatProgram ?? throw new ArgumentNullException(nameof(fileFormatProgram));
		}		
	}
}
