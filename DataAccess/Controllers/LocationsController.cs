using System;
using System.Collections.Generic;
using ELPROCAD.API.Contracts;
using ELPROCAD.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace ELPROCAD.API.Controllers
{
	[ApiController]
	[Route("api/locations")]
	public sealed class LocationsController : ControllerBase
	{
		private readonly ILocationReader _reader;

		public LocationsController(ILocationReader reader)
		{
			_reader = reader ?? throw new ArgumentNullException(nameof(reader));
		}

		[HttpGet]
		public ActionResult<IReadOnlyList<LocationDto>> GetAll()
		{
			return Ok(_reader.GetAll());
		}
	}
}