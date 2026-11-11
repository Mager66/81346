using System;
using System.Collections.Generic;
using ELPROCAD.API.Contracts;
using ELPROCAD.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace ELPROCAD.API.Controllers
{
	[ApiController]
	[Route("api/functions")]
	public sealed class FunctionsController : ControllerBase
	{
		private readonly IFunctionReader _reader;

		public FunctionsController(IFunctionReader reader)
		{
			_reader = reader ?? throw new ArgumentNullException(nameof(reader));
		}

		[HttpGet]
		public ActionResult<IReadOnlyList<FunctionDto>> GetAll()
		{
			return Ok(_reader.GetAll());
		}
	}
}