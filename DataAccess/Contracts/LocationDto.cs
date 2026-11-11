using System;

namespace ELPROCAD.API.Contracts
{
	public sealed class LocationDto
	{
		public Guid Id { get; set; }
		public string Name { get; set; } = string.Empty;
		public Guid? ParentId { get; set; }
		public Guid? RepresentingObjectId { get; set; }
	}
}