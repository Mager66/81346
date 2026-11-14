namespace ELPROCAD.DataAccess.Contracts.MDBX.Objects
{
	/// <summary>
	/// Links a CAD block reference to an engineering object.
	/// The block handle is scoped to its document.
	/// </summary>
	public interface ICadSymbolLink : IEntity
	{
		Guid DocumentId { get; }

		long BlockHandle { get; }

		Guid EngineeringObjectId { get; }

		int? LoadedVersion { get; }

		/// <summary>
		/// Gets the sheet on which the CAD symbol is presented.
		/// </summary>
		Guid SheetId { get; }

		/// <summary>
		/// Gets the time when the symbol was disconnected from its presentation.
		/// </summary>
		DateTime? DetachedAtUtc { get; }

		/// <summary>
		/// Gets whether the symbol link is currently disconnected.
		/// </summary>
		bool IsDetached { get; }
	}
}
