using ELPROCAD.DataAccess.Contracts.MDBX.Aspects;
using ELPROCAD.DataAccess.Contracts.MDBX.Documents;
using ELPROCAD.DataAccess.Contracts.MDBX.Objects;
using ELPROCAD.DataAccess.Contracts.PDSX.Classification;
using ELPROCAD.DataAccess.Contracts.PDSX.Documents;
using ELPROCAD.DataAccess.Contracts.PDSX.ProjectData;
using FluentValidation.Results;

namespace ELPROCAD.DataAccess.Validation
{
	public interface IValidationService
	{
		/// <summary>
		/// Validates project metadata without accessing a database.
		/// </summary>
		ValidationResult Validate(IProject entity);

		/// <summary>
		/// Validates main classification values without accessing a database.
		/// </summary>
		ValidationResult Validate(IMainClass entity);

		/// <summary>
		/// Validates sub-classification values without accessing a database.
		/// </summary>
		ValidationResult Validate(ISubClass entity);

		/// <summary>
		/// Validates document metadata without accessing a database.
		/// </summary>
		ValidationResult Validate(IDocument entity);

		/// <summary>
		/// Validates file format values without accessing a database.
		/// </summary>
		ValidationResult Validate(IFileFormat entity);

		/// <summary>
		/// Validates program configuration without accessing the database or file system.
		/// </summary>
		ValidationResult Validate(IOpeningProgram entity);

		/// <summary>
		/// Validates association values without accessing the database.
		/// </summary>
		ValidationResult Validate(IFileFormatProgram entity);

		/// <summary>
		/// Validates sheet values without accessing the database.
		/// </summary>
		ValidationResult Validate(ISheet entity);

		/// <summary>
		/// Validates values shared by all aspect nodes.
		/// </summary>
		ValidationResult Validate(IAspectNode entity);

		/// <summary>
		/// Validates an object's project and aspect memberships.
		/// </summary>
		ValidationResult Validate(IEngineeringObject entity);
	}
}
