using System;
using System.Collections.Generic;
using ELPROCAD.API.Contracts;

namespace ELPROCAD.API.Services
{
	public interface IDocumentReader
	{
		IReadOnlyList<DocumentDto>? GetByProject(Guid projectId);
	}
}