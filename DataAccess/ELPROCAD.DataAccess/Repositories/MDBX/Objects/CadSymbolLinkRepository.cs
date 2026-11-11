using DevExpress.Xpo;
using ELPROCAD.DataAccess.Contracts.MDBX.Objects;
using ELPROCAD.DataAccess.CRUD;
using ELPROCAD.DataAccess.Database;
using ELPROCAD.DataAccess.Models;
using ELPROCAD.DataAccess.Models.MDBX.Documents;
using ELPROCAD.DataAccess.Models.MDBX.Objects;
using ELPROCAD.DataAccess.Validation;
using ELPROCAD.DataAccess.Validation.MDBX.Objects;
using FluentValidation.Results;

namespace ELPROCAD.DataAccess.Repositories.MDBX.Objects
{
	internal sealed class CadSymbolLinkRepository : ICadSymbolLinkRepository
	{
		private readonly Read _read;
		private readonly Create _create;
		private readonly Delete _delete;
		private readonly CadSymbolLinkValidator _validator = new();

		internal CadSymbolLinkRepository(Read read, Create create, Delete delete)
		{
			_read = read ?? throw new ArgumentNullException(nameof(read));
			_create = create ?? throw new ArgumentNullException(nameof(create));
			_delete = delete ?? throw new ArgumentNullException(nameof(delete));
		}

		public ICadSymbolLink? GetByBlock(Guid documentId, long blockHandle)
		{
			return _read.GetAll<CadSymbolLink>(entity =>
					entity.DocumentId == documentId &&
					entity.BlockHandle == blockHandle)
				.SingleOrDefault();
		}

		public IReadOnlyList<ICadSymbolLink> GetByDocumentId(Guid documentId)
		{
			return _read.GetAll<CadSymbolLink>(
					entity => entity.DocumentId == documentId)
				.OrderBy(entity => entity.BlockHandle)
				.Cast<ICadSymbolLink>()
				.ToList()
				.AsReadOnly();
		}

		public IReadOnlyList<ICadSymbolLink> GetBySheetId(Guid sheetId)
		{
			return _read.GetAll<CadSymbolLink>(
				entity => entity.SheetId == sheetId);
		}

		public IReadOnlyList<ICadSymbolLink> GetByObjectId(
			Guid engineeringObjectId)
		{
			return _read.GetAll<CadSymbolLink>(
					entity => entity.EngineeringObjectId == engineeringObjectId)
				.OrderBy(entity => entity.DocumentId)
				.ThenBy(entity => entity.BlockHandle)
				.Cast<ICadSymbolLink>()
				.ToList()
				.AsReadOnly();
		}

		public (ICadSymbolLink? Value, ValidationResult Validation) Create(
			Guid documentId,
			Guid sheetId,
			long blockHandle,
			Guid engineeringObjectId)
		{
			var input = new CadSymbolLink
			{
				DocumentId = documentId,
				SheetId = sheetId,
				BlockHandle = blockHandle,
				EngineeringObjectId = engineeringObjectId
			};

			var result = _create.New(
				input,
				entity => _validator.Validate(entity),
				CopyLink,
				ValidateReferences);

			return (result.Value, result.Validation);
		}

		public ValidationResult Delete(ICadSymbolLink entity)
		{
			if (entity == null)
				throw new ArgumentNullException(nameof(entity));

			return _delete.Remove<CadSymbolLink>(
				entity.Id,
				entity.LoadedVersion);
		}

		private static ValidationResult ValidateReferences(UnitOfWork uow, CadSymbolLink input)
		{
			var document = uow.GetObjectByKey<Document>(input.DocumentId);

			if (document == null)
			{
				return ValidationResults.Failure(
					nameof(input.DocumentId),
					"The document does not exist.");
			}

			var sheet = uow.GetObjectByKey<Sheet>(input.SheetId);

			if (sheet == null)
			{
				return ValidationResults.Failure(
					nameof(ICadSymbolLink.SheetId),
					"The sheet does not exist.");
			}

			if (sheet.DocumentId != input.DocumentId)
			{
				return ValidationResults.Failure(
					nameof(ICadSymbolLink.SheetId),
					"The sheet belongs to another document.");
			}

			var entity = uow.GetObjectByKey<EngineeringObject>(
				input.EngineeringObjectId);

			if (entity == null)
			{
				return ValidationResults.Failure(
					nameof(input.EngineeringObjectId),
					"The engineering object does not exist.");
			}

			if (entity.IsMarkedForDeletion)
			{
				return ValidationResults.Failure(
					nameof(input.EngineeringObjectId),
					"The engineering object is marked for deletion. Restore it before adding symbols.");
			}

			if (document.ProjectId != entity.ProjectId)
			{
				return ValidationResults.Failure(
					nameof(input.EngineeringObjectId),
					"The document and engineering object must belong to the same project.");
			}

			var duplicate = new XPQuery<CadSymbolLink>(uow)
				.Any(link =>
					link.DocumentId == input.DocumentId &&
					link.BlockHandle == input.BlockHandle);

			if (duplicate)
			{
				return ValidationResults.Failure(
					nameof(input.BlockHandle),
					"The block already has a registered engineering object link.");
			}

			// Protect both references against concurrent deletion.
			DependencyVersions.Touch(
				uow,
				new VersionedEntity[] { document, sheet, entity });

			return new ValidationResult();
		}

		private static void CopyLink(
			UnitOfWork uow,
			CadSymbolLink source,
			CadSymbolLink target)
		{
			target.Id = source.Id;
			target.DocumentId = source.DocumentId;
			target.SheetId = source.SheetId;
			target.BlockHandle = source.BlockHandle;
			target.EngineeringObjectId = source.EngineeringObjectId;
		}
	}
}