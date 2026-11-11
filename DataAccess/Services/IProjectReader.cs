using DevExpress.Xpo;
using ELPROCAD.API.Contracts;
using System;

namespace ELPROCAD.API.Services
{
	public interface IProjectReader 
	{
		IReadOnlyList<ProjectDto> GetAll();
	}

}