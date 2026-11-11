namespace ELPROCAD.DataAccess.Contracts.PDSX.Classification
{
	/// <summary>
	/// Provides common classification data.
	/// </summary>
	public interface IClassification : IEntity
	{
		string Abbrevation { get; }

		string Tooltip { get; }

		string Image { get; }
	}
}
