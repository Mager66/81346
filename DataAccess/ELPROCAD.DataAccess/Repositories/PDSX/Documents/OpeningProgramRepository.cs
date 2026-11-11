using DevExpress.Xpo;
using ELPROCAD.DataAccess.Contracts.PDSX.Documents;
using ELPROCAD.DataAccess.CRUD;
using ELPROCAD.DataAccess.Models.PDSX.Documents;
using ELPROCAD.DataAccess.Validation.PDSX.Documents;
using FluentValidation.Results;

namespace ELPROCAD.DataAccess.Repositories.PDSX.Documents
{
	internal sealed class OpeningProgramRepository : IOpeningProgramRepository
	{
		private readonly Read _read;
		private readonly Create _create;
		private readonly Update _update;
		private readonly OpeningProgramValidator _validator = new();

		internal OpeningProgramRepository(Read read, Create create, Update update)
		{
			_read = read ?? throw new ArgumentNullException(nameof(read));
			_create = create ?? throw new ArgumentNullException(nameof(create));
			_update = update ?? throw new ArgumentNullException(nameof(update));
		}

		public IOpeningProgram? GetById(Guid id) => _read.GetById<OpeningProgram>(id);

		public IReadOnlyList<IOpeningProgram> GetAll()
		{
			return _read.GetAll<OpeningProgram>()
				.OrderBy(
					entity => entity.Name,
					StringComparer.OrdinalIgnoreCase)
				.Cast<IOpeningProgram>()
				.ToList()
				.AsReadOnly();
		}

		public (IOpeningProgram? Value, ValidationResult Validation) Create(string name, string executablePath)
		{
			var input = new OpeningProgram
			{
				Name = name,
				ExecutablePath = executablePath
			};

			var result = _create.New(
				input,
				entity => _validator.Validate(entity),
				CopyOpeningProgram);

			return (result.Value, result.Validation);
		}

		public (IOpeningProgram? Value, ValidationResult Validation) Update(IOpeningProgram entity)
		{
			if (entity == null)
				throw new ArgumentNullException(nameof(entity));

			var input = new OpeningProgram
			{
				Id = entity.Id,
				Name = entity.Name,
				ExecutablePath = entity.ExecutablePath,
				LoadedVersion = entity.LoadedVersion
			};

			var result = _update.Save(
				input,
				item => _validator.Validate(item),
				CopyOpeningProgram);

			return (result.Value, result.Validation);
		}

		private static void CopyOpeningProgram(UnitOfWork unitOfWork, OpeningProgram source, OpeningProgram target)
		{
			target.Id = source.Id;
			target.Name = source.Name;
			target.ExecutablePath = source.ExecutablePath;
		}
	}
}
