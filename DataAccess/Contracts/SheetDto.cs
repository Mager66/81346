using System;

namespace ELPROCAD.API.Contracts
{
	public sealed class SheetDto
	{
		public Guid Id { get; set; }
		public Guid DocumentId { get; set; }
		public string SheetNo { get; set; } = string.Empty;
	}
}