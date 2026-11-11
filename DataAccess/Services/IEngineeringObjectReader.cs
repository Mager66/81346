using System;
using System.Collections.Generic;
using ELPROCAD.API.Contracts;

namespace ELPROCAD.API.Services
{
	public interface IEngineeringObjectReader
	{
		IReadOnlyList<EngineeringObjectDto>? GetByProject(Guid projectId);
	}
}