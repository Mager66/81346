namespace ELPROCAD.DataAccess.Contracts.MDBX.Objects
{
	/// <summary>
	/// Describes an independently identified object with aspect memberships.
	/// </summary>
	public interface IEngineeringObject : IEntity
	{
		Guid ProjectId { get; }

		Guid? SheetId { get; set; }

		Guid? PowerFunctionId { get; set; }

		Guid? ControlFunctionId { get; set; }

		Guid? TopographicStructureId { get; set; }

		Guid? LocationId { get; set; }

		Guid? FunctionId { get; set; }

		Guid? ProductId { get; set; }

		int? LoadedVersion { get; }
	}
}
