namespace ELPROCAD.API.Contracts
{
	public sealed class EngineeringObjectDto
	{
		public Guid Id { get; set; }

		public Guid ProjectId { get; set; }

		public Guid? LocationId { get; set; }

		public Guid? FunctionId { get; set; }
	}
}