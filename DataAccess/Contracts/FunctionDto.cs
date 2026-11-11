using System;

namespace ELPROCAD.API.Contracts
{
	public sealed class FunctionDto
	{
		public Guid Id { get; set; }
		public string Name { get; set; } = string.Empty;
		public Guid? ParentId { get; set; }
		public Guid? RepresentingObjectId { get; set; }
	}
}