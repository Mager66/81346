using DevExpress.Xpo;
using ELPROCAD.DataAccess.Contracts.MDBX.Documents;
using ELPROCAD.DataAccess.CRUD;
using ELPROCAD.DataAccess.Models.MDBX.Documents;
using ELPROCAD.DataAccess.Models.MDBX.Objects;
using ELPROCAD.DataAccess.Validation;
using ELPROCAD.DataAccess.Validation.MDBX.Documents;
using FluentValidation.Results;

namespace ELPROCAD.DataAccess.Repositories.MDBX.Documents
{
	internal sealed class SheetRepository : ISheetRepository
	{
		private readonly Read _read;
		private readonly Create _create;
		private readonly Update _update;
		private readonly Delete _delete;
		private readonly SheetValidator _validator = new();

		internal SheetRepository(Read read, Create create, Update update, Delete delete)
		{
			_read = read ?? throw new ArgumentNullException(nameof(read));
			_create = create ?? throw new ArgumentNullException(nameof(create));
			_update = update ?? throw new ArgumentNullException(nameof(update));
			_delete = delete ?? throw new ArgumentNullException(nameof(delete));
		}

		public ISheet? GetById(Guid id) => _read.GetById<Sheet>(id);

		public IReadOnlyList<ISheet> GetByDocumentId(Guid documentId)
		{
			return _read.GetAll<Sheet>(
					entity => entity.DocumentId == documentId)
				.OrderBy(
					entity => entity.SheetNo,
					StringComparer.OrdinalIgnoreCase)
				.Cast<ISheet>()
				.ToList()
				.AsReadOnly();
		}

		public (ISheet? Value, ValidationResult Validation) Create(Guid documentId, string sheetNo)
		{
			var input = new Sheet
			{
				DocumentId = documentId,
				SheetNo = sheetNo
			};

			var validation = _validator.Validate(input);

			if (!validation.IsValid)
				return (null, validation);

			if (_read.GetById<Document>(documentId) == null)
			{
				return (null, ValidationResults.Failure(
					nameof(ISheet.DocumentId),
					"The document does not exist."));
			}

			var result = _create.New(
				input,
				entity => _validator.Validate(entity),
				CopySheet);

			return (result.Value, result.Validation);
		}

		public (ISheet? Value, ValidationResult Validation) Update(ISheet entity)
		{
			if (entity == null)
				throw new ArgumentNullException(nameof(entity));

			var input = new Sheet
			{
				Id = entity.Id,
				DocumentId = entity.DocumentId,
				SheetNo = entity.SheetNo,
				LoadedVersion = entity.LoadedVersion
			};

			var result = _update.Save(
				input,
				item => _validator.Validate(item),
				CopyChanges);

			return (result.Value, result.Validation);
		}

		public ValidationResult Delete(ISheet entity)
		{
			if (entity == null)
				throw new ArgumentNullException(nameof(entity));

			return _delete.Remove<Sheet>(
				entity.Id,
				entity.LoadedVersion,
				validateDatabase: (unitOfWork, existing) =>
				{
					var hasSymbols = new XPQuery<CadSymbolLink>(unitOfWork)
						.Any(link => link.SheetId == existing.Id);

					return hasSymbols
					? ValidationResults.Failure(
						string.Empty,
						"The sheet cannot be deleted while CAD symbol links reference it.")
					: new ValidationResult();
				});
		}

		private static void CopySheet(UnitOfWork unitOfWork, Sheet source, Sheet target)
		{
			target.Id = source.Id;
			target.DocumentId = source.DocumentId;
			target.SheetNo = source.SheetNo;
		}

		private static void CopyChanges(UnitOfWork unitOfWork, Sheet source, Sheet target)
		{
			// Updating a sheet must not move it to another document.
			target.SheetNo = source.SheetNo;
		}
	}
}
