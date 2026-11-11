using ELPROCAD.DataAccess.Contracts.MDBX.Aspects;
using ELPROCAD.DataAccess.Contracts.MDBX.Objects;
using ELPROCAD.DataAccess.Models.MDBX.Objects;
using ELPROCAD.DataAccess.Repositories.MDBX;
using ELPROCAD.DataAccess.Repositories.PDSX;
using Xunit;

namespace ELPROCAD.DataAccess.Tests.Database
{
	public sealed class EngineeringObjectTests : IDisposable
	{
		private readonly string _directory = Path.Combine(
			Path.GetTempPath(),
			"ELPROCAD.DataAccess.Tests",
			Guid.NewGuid().ToString("N"));

		private readonly IPdsxRepository _pdsx;
		private readonly IMdbxRepository _repository;
		private readonly Guid _projectId;

		public EngineeringObjectTests()
		{
			IDataAccessInitializer initializer = new DataAccessInitializer();

			_pdsx = initializer.InitializePdsx(
				Path.Combine(_directory, "Project.pdsx"));

			var project = _pdsx.Project.Create(
				"TEST-81346",
				_directory,
				"Engineering object tests");

			Assert.True(project.Validation.IsValid);
			_projectId = project.Value!.Id;

			_repository = initializer.InitializeMdbx(
				Path.Combine(_directory, "Project.mdbx"));
		}

		private ILocation AddLocation(string designation)
		{
			var result = _repository.Location.Create(
				_projectId,
				designation);

			Assert.True(result.Validation.IsValid);

			return result.Value!;
		}

		private IEngineeringObject AddObject(Guid locationId)
		{
			var input = _repository.EngineeringObject.New(_projectId);
			input.LocationId = locationId;

			var result = _repository.EngineeringObject.Create(input);

			Assert.True(result.Validation.IsValid);
			Assert.NotNull(result.Value);

			return result.Value!;
		}

		[Fact(DisplayName =	"Creating a draft object does not insert a database record")]
		public void New_ReturnsUnsavedObject()
		{
			var entity = _repository.EngineeringObject.New(_projectId);

			Assert.NotEqual(Guid.Empty, entity.Id);
			Assert.Equal(_projectId, entity.ProjectId);
			Assert.Null(entity.LoadedVersion);

			Assert.Null(_repository.EngineeringObject.GetById(entity.Id));
			Assert.Empty(
				_repository.EngineeringObject.GetByProjectId(_projectId));
		}

		[Fact(DisplayName =	"An engineering object can be persisted without a sheet when it belongs to a location")]
		public void Create_LocationWithoutSheet_IsPersisted()
		{
			var location = AddLocation("AS1");

			var input = _repository.EngineeringObject.New(_projectId);
			input.LocationId = location.Id;

			var result = _repository.EngineeringObject.Create(input);

			Assert.True(result.Validation.IsValid);
			Assert.NotNull(result.Value);
			Assert.Equal(input.Id, result.Value!.Id);
			Assert.NotNull(result.Value.LoadedVersion);
			Assert.Null(result.Value.SheetId);

			var stored = _repository.EngineeringObject.GetById(input.Id);

			Assert.NotNull(stored);
			Assert.Equal(location.Id, stored!.LocationId);

			Assert.Equal(
				input.Id,
				Assert.Single(
					_repository.EngineeringObject.GetByLocationId(location.Id)).Id);

			Assert.Equal(
				input.Id,
				Assert.Single(
					_repository.EngineeringObject.GetByProjectId(_projectId)).Id);

			// Adding a dependency changes the node's concurrency version.
			var currentLocation = _repository.Location.GetById(location.Id)!;

			Assert.NotEqual(location.LoadedVersion, currentLocation.LoadedVersion);
			Assert.False(_repository.Location.Delete(currentLocation).IsValid);
		}

		[Fact(DisplayName =	"An engineering object without an aspect membership is not persisted")]
		public void Create_NoAspect_IsRejected()
		{
			var input = _repository.EngineeringObject.New(_projectId);

			var result = _repository.EngineeringObject.Create(input);

			Assert.Null(result.Value);
			Assert.False(result.Validation.IsValid);
			Assert.Null(_repository.EngineeringObject.GetById(input.Id));
		}

		[Fact(DisplayName =	"An engineering object cannot reference an unknown location")]
		public void Create_UnknownLocation_IsRejected()
		{
			var input = _repository.EngineeringObject.New(_projectId);
			input.LocationId = Guid.NewGuid();

			var result = _repository.EngineeringObject.Create(input);

			Assert.Null(result.Value);

			var error = Assert.Single(result.Validation.Errors);

			Assert.Equal(nameof(IEngineeringObject.LocationId), error.PropertyName);
			Assert.Null(_repository.EngineeringObject.GetById(input.Id));
		}

		[Fact(DisplayName =	"An engineering object cannot use a location belonging to another project")]
		public void Create_LocationFromAnotherProject_IsRejected()
		{
			var location = AddLocation("AS1");

			var otherProject = _pdsx.Project.Create(
				"OTHER",
				_directory,
				"Project boundary test");

			Assert.True(otherProject.Validation.IsValid);

			var input = _repository.EngineeringObject.New(otherProject.Value!.Id);
			input.LocationId = location.Id;

			var result = _repository.EngineeringObject.Create(input);

			Assert.Null(result.Value);

			var error = Assert.Single(result.Validation.Errors);

			Assert.Equal(nameof(IEngineeringObject.LocationId), error.PropertyName);
			Assert.Null(_repository.EngineeringObject.GetById(input.Id));
		}

		[Fact(DisplayName =	"An engineering object can reference a sheet in its project and prevents sheet deletion")]
		public void Create_WithSheet_PreservesReferences()
		{
			var location = AddLocation("AS1");
			var format = _pdsx.FileFormat.GetByExtension(".dwg")!;

			var document = _repository.Document.Create(
				_projectId,
				format.Id,
				"Drawing01",
				Path.Combine(_directory, "Drawing01.dwg"));

			Assert.True(document.Validation.IsValid);

			var sheet = _repository.Sheet.Create(document.Value!.Id, "001");

			Assert.True(sheet.Validation.IsValid);

			var input = _repository.EngineeringObject.New(_projectId);
			input.LocationId = location.Id;
			input.SheetId = sheet.Value!.Id;

			var result = _repository.EngineeringObject.Create(input);

			Assert.True(result.Validation.IsValid);

			Assert.Equal(
				input.Id,
				Assert.Single(
					_repository.EngineeringObject.GetBySheetId(sheet.Value.Id)).Id);

			var currentSheet = _repository.Sheet.GetById(sheet.Value.Id)!;

			Assert.False(_repository.Sheet.Delete(currentSheet).IsValid);
		}

		[Fact(DisplayName =	"A failed sheet reference leaves the object and dependency versions unchanged")]
		public void Create_UnknownSheet_DoesNotPersistChanges()
		{
			var location = AddLocation("AS1");

			var input = _repository.EngineeringObject.New(_projectId);
			input.LocationId = location.Id;
			input.SheetId = Guid.NewGuid();

			var result = _repository.EngineeringObject.Create(input);

			Assert.Null(result.Value);
			Assert.False(result.Validation.IsValid);
			Assert.Null(_repository.EngineeringObject.GetById(input.Id));

			Assert.Equal(
				location.LoadedVersion,
				_repository.Location.GetById(location.Id)!.LoadedVersion);
		}

		#region Update

		[Fact(DisplayName = "Updating location membership preserves object identity and releases the previous location")]
		public void Update_LocationMembership_PreservesIdentity()
		{
			var firstLocation = AddLocation("AS1");
			var secondLocation = AddLocation("AS2");
			var entity = AddObject(firstLocation.Id);

			var id = entity.Id;
			var previousVersion = entity.LoadedVersion;

			entity.LocationId = secondLocation.Id;

			var result = _repository.EngineeringObject.Update(entity);

			Assert.True(result.Validation.IsValid);
			Assert.NotNull(result.Value);
			Assert.Equal(id, result.Value!.Id);
			Assert.NotEqual(previousVersion, result.Value.LoadedVersion);

			var stored = _repository.EngineeringObject.GetById(id)!;

			Assert.Equal(_projectId, stored.ProjectId);
			Assert.Equal(secondLocation.Id, stored.LocationId);

			Assert.Empty(
				_repository.EngineeringObject.GetByLocationId(firstLocation.Id));

			Assert.Equal(
				id,
				Assert.Single(
					_repository.EngineeringObject.GetByLocationId(secondLocation.Id)).Id);

			// The previous location is no longer referenced by this object.
			var unusedLocation = _repository.Location.GetById(firstLocation.Id)!;

			Assert.True(_repository.Location.Delete(unusedLocation).IsValid);
		}

		[Fact(DisplayName =	"Removing the object's final aspect membership is rejected")]
		public void Update_RemovingFinalAspect_IsRejected()
		{
			var location = AddLocation("AS1");
			var entity = AddObject(location.Id);

			entity.LocationId = null;

			var result = _repository.EngineeringObject.Update(entity);

			Assert.Null(result.Value);
			Assert.False(result.Validation.IsValid);

			Assert.Equal(
				location.Id,
				_repository.EngineeringObject.GetById(entity.Id)!.LocationId);
		}

		[Fact(DisplayName =	"Updating to an unknown aspect node preserves the stored object")]
		public void Update_UnknownLocation_IsRejected()
		{
			var location = AddLocation("AS1");
			var entity = AddObject(location.Id);
			var originalVersion = entity.LoadedVersion;

			entity.LocationId = Guid.NewGuid();

			var result = _repository.EngineeringObject.Update(entity);

			Assert.Null(result.Value);

			var error = Assert.Single(result.Validation.Errors);

			Assert.Equal(nameof(IEngineeringObject.LocationId), error.PropertyName);

			var stored = _repository.EngineeringObject.GetById(entity.Id)!;

			Assert.Equal(location.Id, stored.LocationId);
			Assert.Equal(originalVersion, stored.LoadedVersion);
		}

		[Fact(DisplayName =	"An object's project cannot be changed through Update")]
		public void Update_ChangedProject_IsRejected()
		{
			var location = AddLocation("AS1");
			var original = AddObject(location.Id);

			var input = new EngineeringObject
			{
				Id = original.Id,
				ProjectId = Guid.NewGuid(),
				LocationId = original.LocationId,
				LoadedVersion = original.LoadedVersion
			};

			var result = _repository.EngineeringObject.Update(input);

			Assert.Null(result.Value);

			var error = Assert.Single(result.Validation.Errors);

			Assert.Equal(nameof(IEngineeringObject.ProjectId), error.PropertyName);

			Assert.Equal(
				_projectId,
				_repository.EngineeringObject.GetById(original.Id)!.ProjectId);
		}

		[Fact(DisplayName =	"An outdated object cannot overwrite a newer aspect membership")]
		public void Update_OutdatedVersion_IsRejected()
		{
			var firstLocation = AddLocation("AS1");
			var secondLocation = AddLocation("AS2");
			var thirdLocation = AddLocation("AS3");

			var created = AddObject(firstLocation.Id);

			var first = _repository.EngineeringObject.GetById(created.Id)!;
			var outdated = _repository.EngineeringObject.GetById(created.Id)!;

			first.LocationId = secondLocation.Id;

			Assert.True(
				_repository.EngineeringObject.Update(first).Validation.IsValid);

			outdated.LocationId = thirdLocation.Id;

			var result = _repository.EngineeringObject.Update(outdated);

			Assert.Null(result.Value);
			Assert.False(result.Validation.IsValid);

			Assert.Equal(
				secondLocation.Id,
				_repository.EngineeringObject.GetById(created.Id)!.LocationId);

			Assert.Equal(thirdLocation.Id, outdated.LocationId);
		}

		[Fact(DisplayName =	"An object can be attached to and detached from a sheet while retaining its aspect")]
		public void Update_SheetAssociation_CanBeAssignedAndCleared()
		{
			var location = AddLocation("AS1");
			var entity = AddObject(location.Id);
			var format = _pdsx.FileFormat.GetByExtension(".dwg")!;

			var document = _repository.Document.Create(
				_projectId,
				format.Id,
				"Drawing01",
				Path.Combine(_directory, "Drawing01.dwg"));

			Assert.True(document.Validation.IsValid);

			var sheet = _repository.Sheet.Create(document.Value!.Id, "001");

			Assert.True(sheet.Validation.IsValid);

			entity.SheetId = sheet.Value!.Id;

			var attached = _repository.EngineeringObject.Update(entity);

			Assert.True(attached.Validation.IsValid);

			Assert.Equal(
				entity.Id,
				Assert.Single(
					_repository.EngineeringObject.GetBySheetId(sheet.Value.Id)).Id);

			var current = attached.Value!;
			current.SheetId = null;

			var detached = _repository.EngineeringObject.Update(current);

			Assert.True(detached.Validation.IsValid);
			Assert.Null(detached.Value!.SheetId);
			Assert.Equal(location.Id, detached.Value.LocationId);

			Assert.Empty(
				_repository.EngineeringObject.GetBySheetId(sheet.Value.Id));

			var unusedSheet = _repository.Sheet.GetById(sheet.Value.Id)!;

			Assert.True(_repository.Sheet.Delete(unusedSheet).IsValid);
		}

		#endregion Update

		#region Delete

		[Fact(DisplayName =	"Deleting an object preserves its location and removes its membership")]
		public void Delete_Object_PreservesLocation()
		{
			var location = AddLocation("AS1");
			var entity = AddObject(location.Id);

			var result = _repository.EngineeringObject.Delete(entity);

			Assert.True(result.IsValid);
			Assert.Null(_repository.EngineeringObject.GetById(entity.Id));
			Assert.NotNull(_repository.Location.GetById(location.Id));

			Assert.Empty(
				_repository.EngineeringObject.GetByLocationId(location.Id));
		}

		[Fact(DisplayName =	"Deleting an object preserves its sheet and document")]
		public void Delete_Object_PreservesSheetAndDocument()
		{
			var location = AddLocation("AS1");
			var format = _pdsx.FileFormat.GetByExtension(".dwg")!;

			var document = _repository.Document.Create(
				_projectId,
				format.Id,
				"Drawing01",
				Path.Combine(_directory, "Drawing01.dwg"));

			Assert.True(document.Validation.IsValid);

			var sheet = _repository.Sheet.Create(document.Value!.Id, "001");

			Assert.True(sheet.Validation.IsValid);

			var input = _repository.EngineeringObject.New(_projectId);
			input.LocationId = location.Id;
			input.SheetId = sheet.Value!.Id;

			var created = _repository.EngineeringObject.Create(input);

			Assert.True(created.Validation.IsValid);

			var deleted = _repository.EngineeringObject.Delete(created.Value!);

			Assert.True(deleted.IsValid);
			Assert.NotNull(_repository.Sheet.GetById(sheet.Value.Id));
			Assert.NotNull(_repository.Document.GetById(document.Value.Id));
			Assert.Empty(
				_repository.EngineeringObject.GetBySheetId(sheet.Value.Id));
		}

		[Fact(DisplayName =	"An object representing a location cannot be deleted until explicitly unlinked")]
		public void Delete_RepresentingObject_RequiresUnlinking()
		{
			var location = AddLocation("AS1");
			var entity = AddObject(location.Id);

			var currentLocation = _repository.Location.GetById(location.Id)!;

			var assigned = _repository.Location.SetRepresentingObject(
				currentLocation,
				entity.Id);

			Assert.True(assigned.Validation.IsValid);

			// Assigning the representation changed the object's dependency version.
			var currentObject = _repository.EngineeringObject.GetById(entity.Id)!;

			var blocked = _repository.EngineeringObject.Delete(currentObject);

			Assert.False(blocked.IsValid);
			Assert.NotNull(_repository.EngineeringObject.GetById(entity.Id));

			var cleared = _repository.Location.SetRepresentingObject(
				assigned.Value!,
				null);

			Assert.True(cleared.Validation.IsValid);

			var reloaded = _repository.EngineeringObject.GetById(entity.Id)!;
			var deleted = _repository.EngineeringObject.Delete(reloaded);

			Assert.True(deleted.IsValid);
			Assert.Null(_repository.EngineeringObject.GetById(entity.Id));
			Assert.NotNull(_repository.Location.GetById(location.Id));
		}

		[Fact(DisplayName =	"An outdated object cannot delete a subsequently updated record")]
		public void Delete_OutdatedVersion_IsRejected()
		{
			var firstLocation = AddLocation("AS1");
			var secondLocation = AddLocation("AS2");

			var original = AddObject(firstLocation.Id);
			var current = _repository.EngineeringObject.GetById(original.Id)!;

			current.LocationId = secondLocation.Id;

			Assert.True(
				_repository.EngineeringObject.Update(current).Validation.IsValid);

			var result = _repository.EngineeringObject.Delete(original);

			Assert.False(result.IsValid);

			var stored = _repository.EngineeringObject.GetById(original.Id);

			Assert.NotNull(stored);
			Assert.Equal(secondLocation.Id, stored!.LocationId);
		}

		[Fact(DisplayName =	"Deleting an already deleted object returns an identifier validation error")]
		public void Delete_AlreadyDeletedObject_ReturnsFailure()
		{
			var location = AddLocation("AS1");
			var entity = AddObject(location.Id);

			Assert.True(_repository.EngineeringObject.Delete(entity).IsValid);

			var result = _repository.EngineeringObject.Delete(entity);

			Assert.False(result.IsValid);

			var error = Assert.Single(result.Errors);

			Assert.Equal(nameof(IEngineeringObject.Id), error.PropertyName);
		}

		[Fact(DisplayName =	"Deleting an object does not require valid unsaved aspect memberships")]
		public void Delete_InvalidLocalValues_UsesStoredIdentityAndVersion()
		{
			var location = AddLocation("AS1");
			var entity = AddObject(location.Id);

			// Unsaved edits must not prevent deletion of the current stored record.
			entity.LocationId = null;

			var result = _repository.EngineeringObject.Delete(entity);

			Assert.True(result.IsValid);
			Assert.Null(_repository.EngineeringObject.GetById(entity.Id));
		}

		#endregion Delete

		public void Dispose()
		{
			if (Directory.Exists(_directory))
				Directory.Delete(_directory, recursive: true);
		}
	}
}