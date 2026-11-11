using ELPROCAD.DataAccess.Contracts.MDBX.Aspects;
using FluentValidation.Results;

namespace ELPROCAD.DataAccess.Repositories.MDBX.Aspects
{
	public interface ILocationRepository
	{
		/// <summary>
		/// Finds a location by its identifier.
		/// </summary>
		ILocation? GetById(Guid id);

		/// <summary>
		/// Returns root locations for a project, ordered by designation.
		/// </summary>
		IReadOnlyList<ILocation> GetRoots(Guid projectId);

		/// <summary>
		/// Returns the immediate children of a location, ordered by designation.
		/// </summary>
		IReadOnlyList<ILocation> GetChildren(Guid parentId);

		/// <summary>
		/// Returns all project locations ordered by depth and designation.
		/// </summary>
		IReadOnlyList<ILocation> GetByProjectId(Guid projectId);

		/// <summary>
		/// Creates a root or child location and calculates its depth.
		/// </summary>
		(ILocation? Value, ValidationResult Validation) Create(
			Guid projectId,
			string designation,
			Guid? parentId = null);

		/// <summary>
		/// Updates a location's designation using its originally loaded version.
		/// Structural references are preserved.
		/// </summary>
		(ILocation? Value, ValidationResult Validation) Update(ILocation entity);

		/// <summary>
		/// Deletes an unused leaf location using its originally loaded version.
		/// </summary>
		ValidationResult Delete(ILocation entity);

		/// <summary>
		/// Assigns or clears the object representing this location node.
		/// The object's own aspect memberships remain unchanged.
		/// </summary>
		(ILocation? Value, ValidationResult Validation) SetRepresentingObject(
			ILocation location,
			Guid? objectId);

		/// <summary>
		/// Moves a location and its descendants beneath another location.
		/// A null parent moves the location to the root level.
		/// </summary>
		(ILocation? Value, ValidationResult Validation) Move(
			ILocation location,
			Guid? parentId);

		/// <summary>
		/// Changes an existing location's designation path while preserving its identity.
		/// Missing parents are created and unused previous ancestors are removed.
		/// All changes are committed in one transaction.
		/// </summary>
		(ILocation? Value, ValidationResult Validation) ChangePath(
			ILocation location,
			IReadOnlyList<string> targetLevels);
	}
}
