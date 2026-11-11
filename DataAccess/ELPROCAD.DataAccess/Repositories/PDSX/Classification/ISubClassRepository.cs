using ELPROCAD.DataAccess.Contracts.PDSX.Classification;

namespace ELPROCAD.DataAccess.Repositories.PDSX.Classification
{
	public interface ISubClassRepository
	{
		/// <summary>
		/// Finds a sub-classification by its identifier.
		/// </summary>
		ISubClass? GetById(Guid id);

		/// <summary>
		/// Returns the parent's sub-classifications ordered by abbreviation.
		/// </summary>
		IReadOnlyList<ISubClass> GetByMainClassId(Guid mainClassId);
	}
}
