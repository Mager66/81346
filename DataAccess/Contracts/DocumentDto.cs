using System;

namespace ELPROCAD.API.Contracts
{
	public sealed class DocumentDto
	{
		public Guid Id { get; set; }
		public Guid ProjectId { get; set; }
		public string Name { get; set; } = string.Empty;
	}
}