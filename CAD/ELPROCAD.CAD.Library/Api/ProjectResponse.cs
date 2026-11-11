namespace ELPROCAD.CAD.Library.Api
{
	public sealed class ProjectResponse
	{
		public Guid Id { get; set; }

		public string Name { get; set; } = string.Empty;

		public string Path { get; set; } = string.Empty;

		public string Description { get; set; } = string.Empty;
	}
}
