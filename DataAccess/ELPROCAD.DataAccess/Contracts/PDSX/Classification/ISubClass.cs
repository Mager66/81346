namespace ELPROCAD.DataAccess.Contracts.PDSX.Classification
{
	/// <summary>
	/// Provides read-only sub-classification data.
	/// </summary>
	public interface ISubClass : IClassification
	{
		IMainClass MainClass { get; }
	}
}
