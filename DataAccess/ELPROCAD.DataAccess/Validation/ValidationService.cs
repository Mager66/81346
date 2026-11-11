using ELPROCAD.DataAccess.Contracts.MDBX.Aspects;
using ELPROCAD.DataAccess.Contracts.MDBX.Documents;
using ELPROCAD.DataAccess.Contracts.MDBX.Objects;
using ELPROCAD.DataAccess.Contracts.PDSX.Classification;
using ELPROCAD.DataAccess.Contracts.PDSX.Documents;
using ELPROCAD.DataAccess.Contracts.PDSX.ProjectData;
using ELPROCAD.DataAccess.Validation.MDBX.Aspects;
using ELPROCAD.DataAccess.Validation.MDBX.Documents;
using ELPROCAD.DataAccess.Validation.MDBX.Objects;
using ELPROCAD.DataAccess.Validation.PDSX.Classification;
using ELPROCAD.DataAccess.Validation.PDSX.Documents;
using ELPROCAD.DataAccess.Validation.PDSX.ProjectData;
using FluentValidation.Results;

namespace ELPROCAD.DataAccess.Validation
{
	internal sealed class ValidationService : IValidationService
	{
		private readonly ProjectValidator _projectValidator = new();
		private readonly MainClassValidator _mainClassValidator = new();
		private readonly SubClassValidator _subClassValidator = new();
		private readonly FileFormatValidator _fileFormatValidator = new();
		private readonly OpeningProgramValidator _openingProgramValidator = new();
		private readonly FileFormatProgramValidator _fileFormatProgramValidator = new();
		private readonly SheetValidator _sheetValidator = new();
		private readonly AspectNodeValidator _aspectNodeValidator = new();
		private readonly EngineeringObjectValidator _engineeringObjectValidator = new();

		#region mdbx
		private readonly DocumentValidator _documentValidator = new();
		#endregion mdbx

		public ValidationResult Validate(IProject entity)
		{
			if (entity == null)
				throw new ArgumentNullException(nameof(entity));

			return _projectValidator.Validate(entity);
		}

		public ValidationResult Validate(IMainClass entity)
		{
			if (entity == null)
				throw new ArgumentNullException(nameof(entity));

			return _mainClassValidator.Validate(entity);
		}

		public ValidationResult Validate(ISubClass entity)
		{
			if (entity == null)
				throw new ArgumentNullException(nameof(entity));

			return _subClassValidator.Validate(entity);
		}

		public ValidationResult Validate(IDocument entity)
		{
			if (entity == null)
				throw new ArgumentNullException(nameof(entity));

			return _documentValidator.Validate(entity);
		}

		public ValidationResult Validate(IFileFormat entity)
		{
			if (entity == null)
				throw new ArgumentNullException(nameof(entity));

			return _fileFormatValidator.Validate(entity);
		}

		public ValidationResult Validate(IOpeningProgram entity)
		{
			if (entity == null)
				throw new ArgumentNullException(nameof(entity));

			return _openingProgramValidator.Validate(entity);
		}

		public ValidationResult Validate(IFileFormatProgram entity)
		{
			if (entity == null)
				throw new ArgumentNullException(nameof(entity));

			return _fileFormatProgramValidator.Validate(entity);
		}

		public ValidationResult Validate(ISheet entity)
		{
			if (entity == null)
				throw new ArgumentNullException(nameof(entity));

			return _sheetValidator.Validate(entity);
		}

		public ValidationResult Validate(IAspectNode entity)
		{
			if (entity == null)
				throw new ArgumentNullException(nameof(entity));

			return _aspectNodeValidator.Validate(entity);
		}

		public ValidationResult Validate(IEngineeringObject entity)
		{
			if (entity == null)
				throw new ArgumentNullException(nameof(entity));

			return _engineeringObjectValidator.Validate(entity);
		}
	}
}
