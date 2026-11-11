using System;
using System.Collections.Generic;
using System.Linq;
using DevExpress.Xpo;
using ELPROCAD.API.Contracts;
using ELPROCAD.API.Model;

namespace ELPROCAD.API.Services
{
	public sealed class EngineeringObjectReader : IEngineeringObjectReader
	{
		private readonly UnitOfWork _unitOfWork;

		public EngineeringObjectReader(UnitOfWork unitOfWork)
		{
			_unitOfWork = unitOfWork
				?? throw new ArgumentNullException(nameof(unitOfWork));
		}

		public IReadOnlyList<EngineeringObjectDto>? GetByProject(Guid projectId)
		{
			var project = _unitOfWork.GetObjectByKey<Project>(projectId);

			if (project == null)
				return null;

			var objects = new XPQuery<EngineeringObject>(_unitOfWork)
				.Where(item => item.Project == project)
				.ToList();

			return objects
				.OrderBy(item => item.Id)
				.Select(item => new EngineeringObjectDto
				{
					Id = item.Id,
					ProjectId = project.Id,
					LocationId = item.Location?.Id,
					FunctionId = item.Function?.Id
				})
				.ToList()
				.AsReadOnly();
		}
	}
}