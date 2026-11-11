using DevExpress.Data.Filtering;
using DevExpress.Xpo;
using ELPROCAD.API.Model;

namespace ELPROCAD.API.Provider
{
	public class DatabaseTestData
	{
		private const string TestProjectName = "TEST-81346";

		private readonly UnitOfWork _unitOfWork;

		public DatabaseTestData(UnitOfWork unitOfWork) => _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));

		public Guid Create(string projectDirectory)
		{
			var existingProject = _unitOfWork.FindObject<Project>(CriteriaOperator.Parse("[Name] = ?", TestProjectName));

			if(existingProject != null)
				return existingProject.Id;

			var project = new Project(_unitOfWork)
			{
				Name = TestProjectName,
				Path = projectDirectory,
				Description = "Test av objekt och rekursiva strukturer"
			};

			var locationRoot = new Location(_unitOfWork)
			{
				Name = "PLACERING"
			};

			var cabinetLocation = new Location(_unitOfWork)
			{
				Name = "AS1",
				Parent = locationRoot
			};

			var functionRoot = new Function(_unitOfWork)
			{
				Name = "FUNKTION"
			};

			var function = new Function(_unitOfWork)
			{
				Name = "F1",
				Parent = functionRoot
			};

			var cabinet = new EngineeringObject(_unitOfWork)
			{
				Project = project,
				Location = locationRoot
			};

			cabinetLocation.RepresentingObject = cabinet;

			var component = new EngineeringObject(_unitOfWork)
			{
				Project = project,
				Location = cabinetLocation,
				Function = function
			};

			var unassignedObject = new EngineeringObject(_unitOfWork)
			{
				Project = project
			};

			_unitOfWork.CommitChanges();

			return project.Id;
		}
	}

}