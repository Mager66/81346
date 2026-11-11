using ELPROCAD.DataAccess.Repositories.PDSX.Classification;
using ELPROCAD.DataAccess.Repositories.PDSX.Documents;
using ELPROCAD.DataAccess.Repositories.PDSX.ProjectData;

namespace ELPROCAD.DataAccess.Repositories.PDSX
{
	/// <summary>
	/// Provides access to project configuration data.
	/// </summary>
	public interface IPdsxRepository
	{
		IMainClassRepository MainClass { get; }

		ISubClassRepository SubClass { get; }

		IProjectRepository Project { get; }

		IFileFormatRepository FileFormat { get; }

		IOpeningProgramRepository OpeningProgram { get; }

		IFileFormatProgramRepository FileFormatProgram { get; }
	}
}
