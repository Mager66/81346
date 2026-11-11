using DevExpress.Xpo;
using ELPROCAD.DataAccess.Contracts.PDSX.ProjectData;
using ELPROCAD.DataAccess.CRUD;
using ELPROCAD.DataAccess.Models.PDSX.ProjectData;
using ELPROCAD.DataAccess.Validation.PDSX.ProjectData;
using FluentValidation.Results;

namespace ELPROCAD.DataAccess.Repositories.PDSX.ProjectData
{
	internal sealed class ProjectRepository : IProjectRepository
	{
		private readonly Read _read;
		private readonly Create _create;
		private readonly Update _update;
		private readonly ProjectValidator _validator = new();

		internal ProjectRepository(Read read, Create create, Update update)
		{
			_read = read ?? throw new ArgumentNullException(nameof(read));
			_create = create ?? throw new ArgumentNullException(nameof(create));
			_update = update ?? throw new ArgumentNullException(nameof(update));
		}

		public IProject? GetById(Guid id) => _read.GetById<Project>(id);

		public IReadOnlyList<IProject> GetAll()
		{
			return _read.GetAll<Project>()
				.OrderBy(
					entity => entity.Name,
					StringComparer.OrdinalIgnoreCase)
				.Cast<IProject>()
				.ToList()
				.AsReadOnly();
		}

		public (IProject? Value, ValidationResult Validation) Create(string name, string path, string description)
		{
			var entity = new Project
			{
				Name = name,
				Path = path,
				Description = description
			};

			var result = _create.New(
				entity,
				item => _validator.Validate(item),
				CopyProject);

			return (result.Value, result.Validation);
		}

		private static void CopyProject(UnitOfWork unitOfWork, Project source, Project target)
		{
			target.Id = source.Id;
			target.Name = source.Name;
			target.Path = source.Path;
			target.Description = source.Description;
		}

		public (IProject? Value, ValidationResult Validation) Update(IProject entity)
		{
			if (entity == null)
				throw new ArgumentNullException(nameof(entity));

			// Snapshot the supplied values without attaching the caller's instance.
			var input = new Project
			{
				Id = entity.Id,
				Name = entity.Name,
				Path = entity.Path,
				Description = entity.Description,
				LoadedVersion = entity.LoadedVersion
			};

			var result = _update.Save(
				input,
				item => _validator.Validate(item),
				CopyProject);

			return (result.Value, result.Validation);
		}
	}
}
