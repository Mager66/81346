namespace ELPROCAD.DataAccess.Contracts
{
	/// <summary>
	/// Provides the persistent identity of an entity.
	/// </summary>
	public interface IEntity
	{
		Guid Id { get; }
	}
}
