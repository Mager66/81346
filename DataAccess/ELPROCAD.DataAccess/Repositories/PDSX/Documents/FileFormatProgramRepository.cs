using DevExpress.Xpo;
using ELPROCAD.DataAccess.Contracts.PDSX.Documents;
using ELPROCAD.DataAccess.CRUD;
using ELPROCAD.DataAccess.Models.PDSX.Documents;
using ELPROCAD.DataAccess.Validation;
using ELPROCAD.DataAccess.Validation.PDSX.Documents;
using FluentValidation.Results;

namespace ELPROCAD.DataAccess.Repositories.PDSX.Documents
{
	internal sealed class FileFormatProgramRepository : IFileFormatProgramRepository
	{
		private readonly Read _read;
		private readonly Create _create;
		private readonly Update _update;
		private readonly Delete _delete;
		private readonly FileFormatProgramValidator _validator = new();

		internal FileFormatProgramRepository(Read read, Create create, Update update, Delete delete)
		{
			_read = read ?? throw new ArgumentNullException(nameof(read));
			_create = create ?? throw new ArgumentNullException(nameof(create));
			_update = update ?? throw new ArgumentNullException(nameof(update));
			_delete = delete ?? throw new ArgumentNullException(nameof(delete));
		}

		public IFileFormatProgram? GetById(Guid id) => _read.GetById<FileFormatProgram>(id);

		public IReadOnlyList<IFileFormatProgram> GetByFileFormatId(Guid fileFormatId)
		{
			return _read.GetAll<FileFormatProgram>(
					entity => entity.FileFormatId == fileFormatId)
				.Cast<IFileFormatProgram>()
				.ToList()
				.AsReadOnly();
		}

		public (IFileFormatProgram? Value, ValidationResult Validation) Create(Guid fileFormatId, Guid openingProgramId, string arguments)
		{
			var input = new FileFormatProgram
			{
				FileFormatId = fileFormatId,
				OpeningProgramId = openingProgramId,
				Arguments = arguments
			};

			var validation = _validator.Validate(input);

			if (!validation.IsValid)
				return (null, validation);

			if (_read.GetById<FileFormat>(fileFormatId) == null)
			{
				return (null, ValidationResults.Failure(
					nameof(IFileFormatProgram.FileFormatId),
					"The file format does not exist."));
			}

			if (_read.GetById<OpeningProgram>(openingProgramId) == null)
			{
				return (null, ValidationResults.Failure(
					nameof(IFileFormatProgram.OpeningProgramId),
					"The opening program does not exist."));
			}

			var result = _create.New(
				input,
				entity => _validator.Validate(entity),
				CopyAssociation);

			return (result.Value, result.Validation);
		}

		public (IFileFormatProgram? Value, ValidationResult Validation) Update(IFileFormatProgram entity)
		{
			if (entity == null)
				throw new ArgumentNullException(nameof(entity));

			var input = new FileFormatProgram
			{
				Id = entity.Id,
				FileFormatId = entity.FileFormatId,
				OpeningProgramId = entity.OpeningProgramId,
				Arguments = entity.Arguments,
				LoadedVersion = entity.LoadedVersion
			};

			var result = _update.Save(
				input,
				item => _validator.Validate(item),
				CopyChanges);

			return (result.Value, result.Validation);
		}

		public ValidationResult Delete(IFileFormatProgram entity)
		{
			if (entity == null)
				throw new ArgumentNullException(nameof(entity));

			return _delete.Remove<FileFormatProgram>(
				entity.Id,
				entity.LoadedVersion,
				(unitOfWork, existing) =>
				{
					var format = unitOfWork.GetObjectByKey<FileFormat>(
						existing.FileFormatId);

					if (format == null)
					{
						throw new InvalidOperationException(
							"The association references a missing file format.");
					}

					if (format.DefaultProgramAssociationId == existing.Id)
						format.DefaultProgramAssociationId = null;

					// Serialize deletion against concurrent default selection.
					format.ProgramAssociationsRevision =
						checked(format.ProgramAssociationsRevision + 1);

					unitOfWork.Save(format);
				});
		}

		public IFileFormatProgram? GetDefault(Guid fileFormatId)
		{
			var format = _read.GetById<FileFormat>(fileFormatId);

			if (format?.DefaultProgramAssociationId == null)
				return null;

			var association = _read.GetById<FileFormatProgram>(
				format.DefaultProgramAssociationId.Value);

			return association?.FileFormatId == fileFormatId
				? association
				: null;
		}

		public (IFileFormat? Value, ValidationResult Validation) SetDefault(IFileFormat fileFormat,	Guid? associationId)
		{
			if (fileFormat == null)
				throw new ArgumentNullException(nameof(fileFormat));

			var input = new FileFormat
			{
				Id = fileFormat.Id,
				LoadedVersion = fileFormat.LoadedVersion
			};

			var result = _update.Save(
				input,
				// This operation changes only the selection, not editable format metadata.
				_ => new ValidationResult(),
				(unitOfWork, source, target) =>
				{
					target.DefaultProgramAssociationId = associationId;
					target.ProgramAssociationsRevision =
						checked(target.ProgramAssociationsRevision + 1);
				}, 
				(unitOfWork, existing) =>
				{
					if (!associationId.HasValue)
						return new ValidationResult();

					var association =
						unitOfWork.GetObjectByKey<FileFormatProgram>(
							associationId.Value);

					if (association == null)
					{
						return ValidationResults.Failure(
							nameof(IFileFormat.DefaultProgramAssociationId),
							"The program association does not exist.");
					}

					if (association.FileFormatId != existing.Id)
					{
						return ValidationResults.Failure(
							nameof(IFileFormat.DefaultProgramAssociationId),
							"The program association belongs to another file format.");
					}

					return new ValidationResult();
				});

			return (result.Value, result.Validation);
		}

		private static void CopyAssociation(UnitOfWork unitOfWork, FileFormatProgram source, FileFormatProgram target)
		{
			target.Id = source.Id;
			target.FileFormatId = source.FileFormatId;
			target.OpeningProgramId = source.OpeningProgramId;
			target.Arguments = source.Arguments;
		}

		private static void CopyChanges(UnitOfWork unitOfWork, FileFormatProgram source, FileFormatProgram target)
		{
			// Updating arguments must not move an existing association.
			target.Arguments = source.Arguments;
		}

		public (IOpeningConfiguration? Value, ValidationResult Validation)	ResolveDefault(Guid fileFormatId)
		{
			var format = _read.GetById<FileFormat>(fileFormatId);

			if (format == null)
			{
				return (null, ValidationResults.Failure(
					nameof(IFileFormatProgram.FileFormatId),
					"The file format does not exist."));
			}

			if (!format.DefaultProgramAssociationId.HasValue)
			{
				return (null, ValidationResults.Failure(
					nameof(IFileFormat.DefaultProgramAssociationId),
					"No default opening program has been selected for this file format."));
			}

			var association = _read.GetById<FileFormatProgram>(format.DefaultProgramAssociationId.Value);

			if (association == null || association.FileFormatId != format.Id)
			{
				return (null, ValidationResults.Failure(
					nameof(IFileFormat.DefaultProgramAssociationId),
					"The default program association is missing or invalid."));
			}

			var program = _read.GetById<OpeningProgram>(association.OpeningProgramId);

			if (program == null)
			{
				return (null, ValidationResults.Failure(
					nameof(IFileFormatProgram.OpeningProgramId),
					"The associated opening program does not exist."));
			}

			var configuration = new OpeningConfiguration(
				format.Id,
				association.Id,
				program.Id,
				program.Name,
				program.ExecutablePath,
				association.Arguments);

			return (configuration, new ValidationResult());
		}
	}
}
