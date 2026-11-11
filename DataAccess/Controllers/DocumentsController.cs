using System;
using System.Collections.Generic;
using ELPROCAD.API.Contracts;
using ELPROCAD.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace ELPROCAD.API.Controllers
{
	[ApiController]
	[Route("api/projects/{projectId:guid}/documents")]
	public sealed class DocumentsController : ControllerBase
	{
		private readonly IDocumentReader _reader;

		public DocumentsController(IDocumentReader reader)
		{
			_reader = reader ?? throw new ArgumentNullException(nameof(reader));
		}

		[HttpGet]
		public ActionResult<IReadOnlyList<DocumentDto>> GetByProject(
			Guid projectId)
		{
			var documents = _reader.GetByProject(projectId);

			if (documents == null)
				return NotFound();

			return Ok(documents);
		}
	}
}