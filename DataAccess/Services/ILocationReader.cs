using System.Collections.Generic;
using ELPROCAD.API.Contracts;

namespace ELPROCAD.API.Services
{
	public interface ILocationReader
	{
		IReadOnlyList<LocationDto> GetAll();
	}
}