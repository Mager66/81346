using System;
using System.Collections.Generic;
using ELPROCAD.API.Contracts;
using ELPROCAD.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace ELPROCAD.API.Controllers
{
	[ApiController]
	[Route("api/documents/{documentId:guid}/sheets")]
	public sealed class SheetsController : ControllerBase
	{
		private readonly ISheetReader _reader;

		public SheetsController(ISheetReader reader)
		{
			_reader = reader ?? throw new ArgumentNullException(nameof(reader));
		}

		[HttpGet]
		public ActionResult<IReadOnlyList<SheetDto>> GetByDocument(
			Guid documentId)
		{
			var sheets = _reader.GetByDocument(documentId);

			if (sheets == null)
				return NotFound();

			return Ok(sheets);
		}
	}
}