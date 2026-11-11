using ELPROCAD.DataAccess.Contracts.PDSX.Classification;

namespace ELPROCAD.DataAccess.Repositories.PDSX.Classification
{
	public interface IMainClassRepository
	{
		/// <summary>
		/// Finds a main classification by its identifier.
		/// </summary>
		IMainClass? GetById(Guid id);

		/// <summary>
		/// Returns main classifications ordered by abbreviation.
		/// </summary>
		IReadOnlyList<IMainClass> GetAll();
	}
}
