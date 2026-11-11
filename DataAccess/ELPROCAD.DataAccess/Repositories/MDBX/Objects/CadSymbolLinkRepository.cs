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
		private readonly CadSymbolLinkValidator _validator = new();

		internal CadSymbolLinkRepository(Read read, Create create)
		{
			_read = read ?? throw new ArgumentNullException(nameof(read));
			_create = create ?? throw new ArgumentNullException(nameof(create));
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
			long blockHandle,
			Guid engineeringObjectId)
		{
			var input = new CadSymbolLink
			{
				DocumentId = documentId,
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

		private static ValidationResult ValidateReferences(
			UnitOfWork uow,
			CadSymbolLink input)
		{
			var document = uow.GetObjectByKey<Document>(input.DocumentId);

			if (document == null)
			{
				return ValidationResults.Failure(
					nameof(input.DocumentId),
					"The document does not exist.");
			}

			var entity = uow.GetObjectByKey<EngineeringObject>(
				input.EngineeringObjectId);

			if (entity == null)
			{
				return ValidationResults.Failure(
					nameof(input.EngineeringObjectId),
					"The engineering object does not exist.");
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
				new VersionedEntity[] { document, entity });

			return new ValidationResult();
		}

		private static void CopyLink(
			UnitOfWork uow,
			CadSymbolLink source,
			CadSymbolLink target)
		{
			target.Id = source.Id;
			target.DocumentId = source.DocumentId;
			target.BlockHandle = source.BlockHandle;
			target.EngineeringObjectId = source.EngineeringObjectId;
		}
	}
}