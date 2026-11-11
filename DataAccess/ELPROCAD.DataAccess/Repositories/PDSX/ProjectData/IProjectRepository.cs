using ELPROCAD.DataAccess.Contracts.PDSX.ProjectData;
using FluentValidation.Results;

namespace ELPROCAD.DataAccess.Repositories.PDSX.ProjectData
{
	public interface IProjectRepository
	{
		/// <summary>
		/// Finds a project by its identifier.
		/// </summary>
		IProject? GetById(Guid id);

		/// <summary>
		/// Returns projects ordered by name.
		/// </summary>
		IReadOnlyList<IProject> GetAll();

		/// <summary>
		/// Validates and creates a project.
		/// </summary>
		(IProject? Value, ValidationResult Validation) Create(
			string name,
			string path,
			string description);

		/// <summary>
		/// Validates and updates a project using its originally loaded version.
		/// </summary>
		(IProject? Value, ValidationResult Validation) Update(IProject entity);
	}
}
