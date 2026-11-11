using DevExpress.Xpo;
using System;

namespace ELPROCAD.API.Contracts
{
	public class ProjectDto 
	{
		public Guid Id { get; set; }

		public string Name { get; set; } = string.Empty;

		public string Path { get; set; } = string.Empty;

		public string Description { get; set; } = string.Empty;
	}

}