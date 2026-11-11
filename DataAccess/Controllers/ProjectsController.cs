using DevExpress.Xpo;
using ELPROCAD.API.Contracts;
using ELPROCAD.API.Model;
using Microsoft.AspNetCore.Mvc;

namespace ELPROCAD.API.Controllers
{
	[ApiController]
	[Route("api/projects")]
	public sealed class ProjectsController : ControllerBase
	{
		private readonly UnitOfWork _unitOfWork;

		public ProjectsController(UnitOfWork unitOfWork)
		{
			_unitOfWork = unitOfWork;
		}

		[HttpGet]
		public ActionResult<List<ProjectDto>> GetAll()
		{
			var projects = new XPQuery<Project>(_unitOfWork)
				.OrderBy(project => project.Name)
				.ToList();

			return Ok(projects.Select(ToDto).ToList());
		}

		[HttpGet("{id:guid}")]
		public ActionResult<ProjectDto> GetById(Guid id)
		{
			var project = _unitOfWork.GetObjectByKey<Project>(id);

			if (project == null)
				return NotFound();

			return Ok(ToDto(project));
		}

		[HttpPost]
		public ActionResult<ProjectDto> Create(ProjectRequest request)
		{
			var project = new Project(_unitOfWork);

			Apply(project, request);
			_unitOfWork.CommitChanges();

			return CreatedAtAction(
				nameof(GetById),
				new { id = project.Id },
				ToDto(project));
		}

		[HttpPut("{id:guid}")]
		public ActionResult<ProjectDto> Update(Guid id, ProjectRequest request)
		{
			var project = _unitOfWork.GetObjectByKey<Project>(id);

			if (project == null)
				return NotFound();

			Apply(project, request);
			_unitOfWork.CommitChanges();

			return Ok(ToDto(project));
		}

		[HttpDelete("{id:guid}")]
		public IActionResult Delete(Guid id)
		{
			var project = _unitOfWork.GetObjectByKey<Project>(id);

			if (project == null)
				return NotFound();

			var hasDocuments = new XPQuery<Document>(_unitOfWork)
				.Any(document => document.Project == project);

			var hasObjects = new XPQuery<EngineeringObject>(_unitOfWork)
				.Any(item => item.Project == project);

			if (hasDocuments || hasObjects)
			{
				return Conflict(new
				{
					message =
						"Projektet innehåller dokument eller objekt och kan inte tas bort."
				});
			}


			_unitOfWork.Delete(project);
			_unitOfWork.CommitChanges();

			return NoContent();
		}

		private static void Apply(Project project, ProjectRequest request)
		{
			project.Name = request.Name;
			project.Path = request.Path;
			project.Description = request.Description ?? string.Empty;
		}

		private static ProjectDto ToDto(Project project)
		{
			return new ProjectDto
			{
				Id = project.Id,
				Name = project.Name,
				Path = project.Path,
				Description = project.Description
			};
		}
	}

}