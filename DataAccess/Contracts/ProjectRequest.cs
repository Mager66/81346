using System.ComponentModel.DataAnnotations;

namespace ELPROCAD.API.Contracts
{
	public sealed class ProjectRequest
	{
		[Required]
		[StringLength(50)]
		public string Name { get; set; } = string.Empty;

		[Required]
		[StringLength(255)]
		public string Path { get; set; } = string.Empty;

		[StringLength(255)]
		public string Description { get; set; } = string.Empty;
	}
}