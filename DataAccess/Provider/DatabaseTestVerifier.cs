using DevExpress.Data.Filtering;
using DevExpress.Xpo;
using ELPROCAD.API.Model;

namespace ELPROCAD.API.Provider
{
	public class DatabaseTestVerifier 
	{
		private readonly UnitOfWork _unitOfWork;

		public DatabaseTestVerifier(UnitOfWork unitOfWork)
		{
			_unitOfWork = unitOfWork
				?? throw new ArgumentNullException(nameof(unitOfWork));
		}

		public void Verify(string projectName)
		{
			var project = _unitOfWork.FindObject<Project>(
				CriteriaOperator.Parse("[Name] = ?", projectName));

			Require(project != null, "Testprojektet saknas.");

			var objects = new XPQuery<EngineeringObject>(_unitOfWork)
				.Where(item => item.Project == project)
				.ToList();

			Require(objects.Count == 3, "Projektet ska innehålla tre objekt.");

			var components = objects
				.Where(item =>
					item.Location != null &&
					item.Function != null)
				.ToList();

			Require(
				components.Count == 1,
				"Ett objekt ska tillhöra både placering och funktion.");

			var component = components[0];
			var location = component.Location!;
			var function = component.Function!;

			Require(location.Name == "AS1", "Komponentens placering ska vara AS1.");
			Require(function.Name == "F1", "Komponentens funktion ska vara F1.");

			var locationRoot = location.Parent;
			var functionRoot = function.Parent;

			Require(locationRoot != null, "AS1 saknar parent.");
			Require(functionRoot != null, "F1 saknar parent.");

			Require(
				locationRoot!.Name == "PLACERING" &&
				locationRoot.Parent == null,
				"Placeringsroten är felaktig.");

			Require(
				functionRoot!.Name == "FUNKTION" &&
				functionRoot.Parent == null,
				"Funktionsroten är felaktig.");

			Require(
			locationRoot.Children.Any(child => child.Id == location.Id),
			"AS1 saknas i placeringsrotens Children.");

			Require(
				functionRoot.Children.Any(child => child.Id == function.Id),
				"F1 saknas i funktionsrotens Children.");

			Require(
				location.Objects.Any(item => item.Id == component.Id),
				"Komponenten saknas i AS1:s Objects.");

			Require(
				function.Objects.Any(item => item.Id == component.Id),
				"Komponenten saknas i F1:s Objects.");

			var cabinet = location.RepresentingObject;

			Require(cabinet != null, "AS1 saknar representerande objekt.");

			Require(
				objects.Any(item => item.Id == cabinet!.Id),
				"Skåpobjektet tillhör inte testprojektet.");

			Require(
				cabinet!.Id != component.Id,
				"Skåpet och komponenten ska vara olika objekt.");

			Require(
				cabinet.Location?.Id == locationRoot.Id,
				"Skåpobjektet ska tillhöra placeringsroten.");

			Require(
				locationRoot.Objects.Any(item => item.Id == cabinet.Id),
				"Skåpobjektet saknas i placeringsrotens Objects.");

			var unassigned = objects
				.Where(item =>
					item.Location == null &&
					item.Function == null)
				.ToList();

			Require(
				unassigned.Count == 1,
				"Ett objekt ska sakna strukturtillhörighet.");

			var ids = objects
				.Select(item => item.Id)
				.Concat(new[]
				{
					project!.Id,
					location.Id,
					locationRoot.Id,
					function.Id,
					functionRoot.Id
				})
				.ToList();

			Require(
				ids.All(id => id != Guid.Empty),
				"En eller flera poster saknar GUID.");

			Require(
				ids.Distinct().Count() == ids.Count,
				"Två testposter har samma GUID.");
		}

		private static void Require(bool condition, string message)
		{
			if (!condition)
				throw new InvalidOperationException(
					$"Databasverifiering misslyckades: {message}");
		}

	}

}