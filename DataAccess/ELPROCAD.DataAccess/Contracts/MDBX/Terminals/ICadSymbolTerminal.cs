namespace ELPROCAD.DataAccess.Contracts.MDBX.Objects
{
	/// <summary>Maps a symbol attribute slot to an object's terminal, connected or not.</summary>
	public interface ICadSymbolTerminal : IEntity
	{
		System.Guid CadSymbolLinkId { get; }
		System.Guid EngineeringObjectTerminalId { get; }
		string Tag { get; }
		int? LoadedVersion { get; }
	}
}

