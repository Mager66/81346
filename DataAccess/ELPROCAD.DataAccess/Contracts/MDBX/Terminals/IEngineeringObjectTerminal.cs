namespace ELPROCAD.DataAccess.Contracts.MDBX.Objects
{
	/// <summary>Identifies a terminal belonging to one engineering object.</summary>
	public interface IEngineeringObjectTerminal : IEntity
	{
		System.Guid EngineeringObjectId { get; }
		string Designation { get; }
		int? LoadedVersion { get; }

		int? ConfiguredMaxWireConnections { get; }

		int MaxWireConnections { get; }
	}
}

