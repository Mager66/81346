using DevExpress.Xpo;
using ELPROCAD.DataAccess.Contracts.PDSX.Documents;
using ELPROCAD.DataAccess.CRUD;
using ELPROCAD.DataAccess.Models.PDSX.Documents;
using ELPROCAD.DataAccess.Validation.PDSX.Documents;
using FluentValidation.Results;

namespace ELPROCAD.DataAccess.Repositories.PDSX.Documents
{
	internal sealed class FileFormatRepository : IFileFormatRepository
	{
		private readonly Read _read;
		private readonly Create _create;
		private readonly Update _update;
		private readonly FileFormatValidator _validator = new();

		internal FileFormatRepository(Read read, Create create, Update update)
		{
			_read = read ?? throw new ArgumentNullException(nameof(read));
			_create = create ?? throw new ArgumentNullException(nameof(create));
			_update = update ?? throw new ArgumentNullException(nameof(update));
		}

		public IFileFormat? GetById(Guid id) => _read.GetById<FileFormat>(id);

		public IFileFormat? GetByExtension(string extension)
		{
			var normalized = FileExtensions.Normalize(extension);

			if(normalized.Length == 0)
				return null;

			return _read.GetAll<FileFormat>(
				entity => entity.Extension == normalized)
				.SingleOrDefault();
		}

		public IReadOnlyList<IFileFormat> GetAll()
		{
			return _read.GetAll<FileFormat>()
				.OrderBy(
					entity => entity.Name,
					StringComparer.OrdinalIgnoreCase)
				.Cast<IFileFormat>()
				.ToList()
				.AsReadOnly();
		}

		public (IFileFormat? Value, ValidationResult Validation) Create(string name, string description, string extension)
		{
			var input = new FileFormat
			{
				Name = name,
				Description = description,
				Extension = FileExtensions.Normalize(extension)
			};

			var result = _create.New(
				input,
				entity => _validator.Validate(entity),
				CopyFileFormat);

			return (result.Value, result.Validation);
		}

		public(IFileFormat? Value, ValidationResult Validation) Update(IFileFormat entity)
		{
			if(entity == null) 
				throw new ArgumentNullException(nameof(entity));

			var input = new FileFormat
			{
				Id = entity.Id,
				Name = entity.Name,
				Description = entity.Description,
				Extension = FileExtensions.Normalize(entity.Extension),
				LoadedVersion = entity.LoadedVersion
			};

			var result = _update.Save(
				input,
				entity => _validator.Validate(entity),
				CopyFileFormat);

			return (result.Value, result.Validation);
		}

		private static void CopyFileFormat(UnitOfWork unitOfWork, FileFormat source, FileFormat target)
		{
			target.Id = source.Id;
			target.Name = source.Name;
			target.Description = source.Description;
			target.Extension = source.Extension;
		}
	}
}
