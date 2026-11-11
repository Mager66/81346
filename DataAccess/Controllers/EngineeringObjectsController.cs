using System;
using System.Collections.Generic;
using ELPROCAD.API.Contracts;
using ELPROCAD.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace ELPROCAD.API.Controllers
{
	[ApiController]
	[Route("api/projects/{projectId:guid}/objects")]
	public sealed class EngineeringObjectsController : ControllerBase
	{
		private readonly IEngineeringObjectReader _objectReader;

		public EngineeringObjectsController(
			IEngineeringObjectReader objectReader)
		{
			_objectReader = objectReader
				?? throw new ArgumentNullException(nameof(objectReader));
		}

		[HttpGet]
		public ActionResult<IReadOnlyList<EngineeringObjectDto>> GetByProject(
			Guid projectId)
		{
			var objects = _objectReader.GetByProject(projectId);

			if (objects == null)
				return NotFound();

			return Ok(objects);
		}
	}
}