using DevExpress.Xpo;
using ELPROCAD.CAD.Data.Model;

namespace ELPROCAD.CAD.Data.CRUD.Analisys
{
	public sealed class QueryProject
	{
		private readonly UnitOfWork _unitOfWork;

		public QueryProject(UnitOfWork unitOfWork) => _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));

		public Project? GetProjectById(Guid id) => _unitOfWork.GetObjectByKey<Project>(id);

		public IReadOnlyList<Project> GetAll()
		{
			return new XPQuery<Project>(_unitOfWork)
			.OrderBy(project => project.Name)
			.ToList()
			.AsReadOnly();			
		}

		public Project Add(string name, string path, string description)
		{
			var project = new Project(_unitOfWork)
			{
				Name = name,
				Path = path,
				Description = description
			};

			_unitOfWork.CommitChanges();

			return project;
		}

		public Project Update(Guid id, string name, string path, string description)
		{
			var project = GetProjectById(id)
				?? throw new KeyNotFoundException(
					$"Projektet '{id}' finns inte.");

			project.Name = name;
			project.Path = path;
			project.Description = description;

			_unitOfWork.CommitChanges();

			return project;
		}

		public bool Delete(Guid id)
		{
			var project = GetProjectById(id);

			if (project == null)
				return false;

			var hasDocuments = new XPQuery<Document>(_unitOfWork)
				.Any(document => document.Project == project);

			var hasObjects = new XPQuery<EngineeringObject>(_unitOfWork)
				.Any(item => item.Project == project);

			if (hasDocuments || hasObjects)
			{
				throw new InvalidOperationException(
					"Projektet innehåller dokument eller objekt.");
			}

			_unitOfWork.Delete(project);
			_unitOfWork.CommitChanges();

			return true;
		}
	}
}
