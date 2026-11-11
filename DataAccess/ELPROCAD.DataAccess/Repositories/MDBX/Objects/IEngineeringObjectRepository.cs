using ELPROCAD.DataAccess.Contracts.MDBX.Objects;
using FluentValidation.Results;

namespace ELPROCAD.DataAccess.Repositories.MDBX.Objects
{
	public interface IEngineeringObjectRepository
	{
		/// <summary>
		/// Creates an unsaved object for editing without accessing the database.
		/// </summary>
		IEngineeringObject New(Guid projectId);

		/// <summary>
		/// Validates and persists an object with at least one existing aspect membership.
		/// </summary>
		(IEngineeringObject? Value, ValidationResult Validation) Create(
			IEngineeringObject entity);

		/// <summary>
		/// Finds an object by its stable identifier.
		/// </summary>
		IEngineeringObject? GetById(Guid id);

		/// <summary>
		/// Returns the objects belonging to a project.
		/// </summary>
		IReadOnlyList<IEngineeringObject> GetByProjectId(Guid projectId);

		/// <summary>
		/// Returns the objects associated with a sheet.
		/// </summary>
		IReadOnlyList<IEngineeringObject> GetBySheetId(Guid sheetId);

		/// <summary>
		/// Returns the objects belonging to a location node.
		/// </summary>
		IReadOnlyList<IEngineeringObject> GetByLocationId(Guid locationId);

		/// <summary>
		/// Updates memberships and sheet association using the originally loaded version.
		/// The object's identity and project are preserved.
		/// </summary>
		(IEngineeringObject? Value, ValidationResult Validation) Update(
			IEngineeringObject entity);

		/// <summary>
		/// Marks the object for deletion while preserving its identity and relations.
		/// Uses the originally loaded version.
		/// </summary>
		ValidationResult Delete(IEngineeringObject entity);

		/// <summary>
		/// Restores a deletion-marked object using its originally loaded version.
		/// Preserves its identity and relations.
		/// </summary>
		(IEngineeringObject? Value, ValidationResult Validation) Restore(IEngineeringObject entity);
	}
}
