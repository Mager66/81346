using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ELPROCAD.CAD.Library.Analysis.Sheets;
using ELPROCAD.CAD.Library.CadEntity;
using ELPROCAD.DataAccess.Contracts.MDBX.Aspects;
using ELPROCAD.DataAccess.Contracts.MDBX.Documents;
using ELPROCAD.DataAccess.Contracts.MDBX.Objects;
using ELPROCAD.DataAccess.Repositories.MDBX;
using ELPROCAD.DataAccess.Repositories.PDSX;
using FluentValidation.Results;

#if ARX_APP
using AcAp = Autodesk.AutoCAD.ApplicationServices;
using AcDb = Autodesk.AutoCAD.DatabaseServices;
#elif BRX_APP
using AcAp = Bricscad.ApplicationServices;
using AcDb = Teigha.DatabaseServices;
#endif

namespace ELPROCAD.CAD.Data.Analysis.Import
{
	public sealed partial class AnalysisBlockImporter
	{

		private IEngineeringObject GetOrCreateObject(Guid projectId, Guid locationId)
		{
			// Reload because creating children or objects changes dependency versions.
			var location = _mdbx.Location.GetById(locationId)
				?? throw new InvalidOperationException(
					"The location no longer exists.");

			if (location.RepresentedByObjectId.HasValue)
			{
				var existing = _mdbx.EngineeringObject.GetById(
					location.RepresentedByObjectId.Value)
					?? throw new InvalidOperationException(
						"The location references an object that no longer exists.");

				EnsureObjectIsActive(existing);

				if (existing.ProjectId != projectId ||
					existing.LocationId != location.Id)
				{
					throw new InvalidOperationException(
						"The existing object does not match the imported location.");
				}

				return existing;
			}

			// Do not guess which unlinked object represents the location.
			if (_mdbx.EngineeringObject.GetByLocationId(location.Id).Count > 0)
			{
				throw new InvalidOperationException(
					$"Location '{location.Designation}' contains objects but has no " +
					"representing object. Resolve that association before importing.");
			}

			var entity = _mdbx.EngineeringObject.New(projectId);
			entity.LocationId = location.Id;

			var created = Require(_mdbx.EngineeringObject.Create(entity));

			// Object creation changes the location's dependency version.
			location = _mdbx.Location.GetById(location.Id)
				?? throw new InvalidOperationException(
					"The location no longer exists.");

			Require(_mdbx.Location.SetRepresentingObject(location, created.Id));

			return created;
		}

		private IEngineeringObject GetContainingObject(Guid locationId)
		{
			var location = _mdbx.Location.GetById(locationId)
				?? throw new InvalidOperationException(
					"The location no longer exists.");

			if (!location.RepresentedByObjectId.HasValue)
			{
				throw new InvalidOperationException(
					$"Location '{location.Designation}' has no representing object. " +
					"Import its main symbol before its member symbols.");
			}

			var entity = _mdbx.EngineeringObject.GetById(
				location.RepresentedByObjectId.Value)
				?? throw new InvalidOperationException(
					"The representing object no longer exists.");

			EnsureObjectIsActive(entity);

			return entity;
		}

		/// <summary>
		/// Resolves the location represented by an existing engineering object.
		/// </summary>
		private ILocation GetRepresentedLocation(IEngineeringObject entity, Guid projectId)
		{
			EnsureObjectIsActive(entity);

			if (entity.ProjectId != projectId)
				throw new InvalidOperationException("The engineering object belongs to another project.");

			if (!entity.LocationId.HasValue)
				throw new InvalidOperationException("The engineering object has no location membership.");

			var location = _mdbx.Location.GetById(entity.LocationId.Value)
				?? throw new InvalidOperationException("The object's location no longer exists.");

			if (location.ProjectId != projectId || location.RepresentedByObjectId != entity.Id)
				throw new InvalidOperationException("The engineering object does not represent its assigned location.");

			return location;
		}

		private static void EnsureObjectIsActive(IEngineeringObject entity)
		{
			if (entity.IsMarkedForDeletion)
			{
				throw new InvalidOperationException(
					$"Engineering object '{entity.Id}' is marked for deletion. " +
					"The CAD symbol has been preserved. " +
					"Restore the object or explicitly confirm removal of the symbol.");
			}
		}

		private static T Require<T>((T? Value, ValidationResult Validation) result) where T : class
		{
			if (!result.Validation.IsValid)
			{
				throw new InvalidOperationException(
					string.Join(
						Environment.NewLine,
						result.Validation.Errors
							.Select(error => error.ErrorMessage)
							.Distinct()));
			}

			return result.Value
				?? throw new InvalidOperationException(
					"The operation succeeded without returning a value.");
		}
	}
}

