using System;
using System.Collections.Generic;
using ELPROCAD.API.Contracts;

namespace ELPROCAD.API.Services
{
	public interface ISheetReader
	{
		IReadOnlyList<SheetDto>? GetByDocument(Guid documentId);
	}
}