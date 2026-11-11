using DevExpress.Xpo;
using ELPROCAD.API.Contracts;
using ELPROCAD.API.Model;
using System;

namespace ELPROCAD.API.Services
{
	public class ProjectReader : IProjectReader
	{
		private readonly UnitOfWork _unitOfWork;

		public ProjectReader(UnitOfWork unitOfWork) => _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));

		public IReadOnlyList<ProjectDto> GetAll()
		{
			var projects = new XPQuery<Project>(_unitOfWork)
				.OrderBy(p=>p.Name)
				.ToList();

			return projects.Select(p => new ProjectDto
			{
				Id = p.Id,
				Name = p.Name,
				Path = p.Path,
				Description = p.Description,
			}).ToList().AsReadOnly();
		}
	}

}