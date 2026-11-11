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
	internal sealed class DocumentRepository : IDocumentRepository
	{
		private readonly Read _read;
		private readonly Create _create;
		private readonly Update _update;
		private readonly Delete _delete;
		private readonly DocumentValidator _validator = new();

		internal DocumentRepository(Read read, Create create, Update update, Delete delete)
		{
			_read = read ?? throw new ArgumentNullException(nameof(read));
			_create = create ?? throw new ArgumentNullException(nameof(create));
			_update = update ?? throw new ArgumentNullException(nameof(update));
			_delete = delete ?? throw new ArgumentNullException(nameof(delete));
		}

		public IDocument? GetById(Guid id) => _read.GetById<Document>(id);

		public IReadOnlyList<IDocument> GetAll()
		{
			return _read.GetAll<Document>()
				.OrderBy(
					entity => entity.Name,
					StringComparer.OrdinalIgnoreCase)
				.Cast<IDocument>()
				.ToList()
				.AsReadOnly();
		}

		public (IDocument? Value, ValidationResult Validation) Create(Guid projectId, Guid FileFormatId, string name, string path)
		{
			var entity = new Document
			{
				ProjectId = projectId,
				FileFormatId = FileFormatId,
				Name = name,
				Path = path
			};

			var result = _create.New(
				entity,
				item => _validator.Validate(item),
				CopyDocument);

			return (result.Value, result.Validation);
		}

		public (IDocument? Value, ValidationResult Validation) Update(IDocument entity)
		{
			if (entity == null)
				throw new ArgumentNullException(nameof(entity));

			// Snapshot the input without attaching the caller's instance.
			var input = new Document
			{
				Id = entity.Id,
				ProjectId = entity.ProjectId,
				FileFormatId = entity.FileFormatId,
				Name = entity.Name,
				Path = entity.Path,
				LoadedVersion = entity.LoadedVersion
			};

			var result = _update.Save(
				input,
				item => _validator.Validate(item),
				CopyDocumentChanges);

			return (result.Value, result.Validation);
		}

		public ValidationResult Delete(IDocument entity)
		{
			if (entity == null)
				throw new ArgumentNullException(nameof(entity));

			return _delete.Remove<Document>(
				entity.Id,
				entity.LoadedVersion,
				validateDatabase: (unitOfWork, existing) =>
				{
					var hasSymbolLinks = new XPQuery<CadSymbolLink>(unitOfWork)
						.Any(link => link.DocumentId == existing.Id);

					if (hasSymbolLinks)
					{
						return ValidationResults.Failure(
							string.Empty,
							"The document cannot be deleted while CAD symbols reference it.");
					}

					var hasRelatedData =
						new XPQuery<Sheet>(unitOfWork)
							.Any(item => item.DocumentId == existing.Id)
						||
						new XPQuery<CadDocumentSpace>(unitOfWork)
							.Any(item => item.DocumentId == existing.Id)
						||
						new XPQuery<DocumentProperties>(unitOfWork)
							.Any(item => item.DocumentId == existing.Id)
						||
						new XPQuery<CadDocumentCustomProperty>(unitOfWork)
							.Any(item => item.DocumentId == existing.Id);


					return hasRelatedData
						? ValidationResults.Failure(
							string.Empty,
							"The document cannot be deleted while it contains related records.")
						: new ValidationResult();
				});
		}

		private static void CopyDocument(UnitOfWork unitOfWork, Document source, Document target)
		{
			target.Id = source.Id;
			target.ProjectId = source.ProjectId;
			target.FileFormatId = source.FileFormatId;
			target.Name = source.Name;
			target.Path = source.Path;
		}

		/// <summary>
		/// Copies editable metadata while preserving identity and project ownership.
		/// </summary>
		private static void CopyDocumentChanges(UnitOfWork unitOfWork, Document source,	Document target)
		{
			target.Name = source.Name;
			target.Path = source.Path;
		}
	}
}
