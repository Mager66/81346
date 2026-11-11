using DevExpress.Xpo;
using ELPROCAD.DataAccess.Changes.Locations;
using ELPROCAD.DataAccess.Contracts.MDBX.Aspects;
using ELPROCAD.DataAccess.Contracts.MDBX.Objects;
using ELPROCAD.DataAccess.CRUD;
using ELPROCAD.DataAccess.Database;
using ELPROCAD.DataAccess.Models.MDBX.Aspects;
using ELPROCAD.DataAccess.Models.MDBX.Objects;
using ELPROCAD.DataAccess.Repositories.MDBX;
using ELPROCAD.DataAccess.Repositories.MDBX.Aspects;
using ELPROCAD.DataAccess.Repositories.PDSX;
using FluentValidation.Results;
using Xunit;

namespace ELPROCAD.DataAccess.Tests.Database
{
	public sealed class LocationTests : IDisposable
	{
		private readonly string _directory = Path.Combine(
			Path.GetTempPath(),
			"ELPROCAD.DataAccess.Tests",
			Guid.NewGuid().ToString("N"));

		private readonly IDatabaseAccess _database;
		private readonly IMdbxRepository _repository;
		private readonly IPdsxRepository _pdsx;
		private readonly Guid _projectId;

		public LocationTests()
		{
			IDataAccessInitializer initializer = new DataAccessInitializer();

			_pdsx = initializer.InitializePdsx(
				Path.Combine(_directory, "Project.pdsx"));

			var project = _pdsx.Project.Create(
				"TEST-81346",
				_directory,
				"Location tests");

			Assert.True(project.Validation.IsValid);
			_projectId = project.Value!.Id;

			_repository = initializer.InitializeMdbx(
				Path.Combine(_directory, "Project.mdbx"));

			_database = new DatabaseAccess(
				DatabaseConnectionStrings.Create(
					Path.Combine(_directory, "Project.mdbx")));
		}

		private ILocation Add(string designation, Guid? parentId = null)
		{
			var result = _repository.Location.Create(
				_projectId,
				designation,
				parentId);

			Assert.True(result.Validation.IsValid);
			Assert.NotNull(result.Value);

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

		[Fact(DisplayName =	"Creating locations calculates depth and preserves parent relationships")]
		public void Create_RootAndChildren_CalculatesDepth()
		{
			var root = Add("AS1");
			var child = Add("B5", root.Id);
			var grandchild = Add("K1", child.Id);

			Assert.Null(root.ParentId);
			Assert.Equal(0, root.Depth);

			Assert.Equal(root.Id, child.ParentId);
			Assert.Equal(1, child.Depth);

			Assert.Equal(child.Id, grandchild.ParentId);
			Assert.Equal(2, grandchild.Depth);

			var stored = _repository.Location.GetById(grandchild.Id);

			Assert.NotNull(stored);
			Assert.Equal(_projectId, stored!.ProjectId);
			Assert.Equal("K1", stored.Designation);
			Assert.Equal(child.Id, stored.ParentId);
			Assert.Equal(2, stored.Depth);
			Assert.NotNull(stored.LoadedVersion);
		}

		[Fact(DisplayName =	"Location queries distinguish roots, immediate children and all project nodes")]
		public void Read_ReturnsExpectedTreeLevels()
		{
			var secondRoot = Add("AS2");
			var firstRoot = Add("AS1");
			var secondChild = Add("K2", firstRoot.Id);
			var firstChild = Add("K1", firstRoot.Id);

			Add("B1", firstChild.Id);

			Assert.Equal(
				new[] { "AS1", "AS2" },
				_repository.Location.GetRoots(_projectId)
					.Select(entity => entity.Designation));

			Assert.Equal(
				new[] { "K1", "K2" },
				_repository.Location.GetChildren(firstRoot.Id)
					.Select(entity => entity.Designation));

			Assert.Empty(_repository.Location.GetChildren(secondRoot.Id));
			Assert.Empty(_repository.Location.GetChildren(secondChild.Id));

			Assert.Equal(
				5,
				_repository.Location.GetByProjectId(_projectId).Count);

			Assert.Null(_repository.Location.GetById(Guid.NewGuid()));
		}

		[Fact(DisplayName =	"The same designation is rejected at the same depth even under different parents")]
		public void Create_SameDepthDifferentParents_IsRejected()
		{
			var firstRoot = Add("AS1");
			var secondRoot = Add("AS2");

			var original = Add("K1", firstRoot.Id);

			var result = _repository.Location.Create(
				_projectId,
				"K1",
				secondRoot.Id);

			Assert.Null(result.Value);
			Assert.False(result.Validation.IsValid);

			var error = Assert.Single(result.Validation.Errors);

			Assert.Equal(nameof(ILocation.Designation), error.PropertyName);

			Assert.Equal(
				original.Id,
				Assert.Single(
					_repository.Location.GetChildren(firstRoot.Id)).Id);

			Assert.Empty(_repository.Location.GetChildren(secondRoot.Id));
		}

		[Fact(DisplayName =	"The same designation is allowed at different depths")]
		public void Create_SameDesignationDifferentDepths_IsAllowed()
		{
			var root = Add("AS1");

			var direct = Add("K1", root.Id);
			var intermediate = Add("B5", root.Id);
			var nested = Add("K1", intermediate.Id);

			Assert.Equal(1, direct.Depth);
			Assert.Equal(2, nested.Depth);
			Assert.NotEqual(direct.Id, nested.Id);
		}

		[Fact(DisplayName =	"An unknown parent location is rejected")]
		public void Create_UnknownParent_IsRejected()
		{
			var result = _repository.Location.Create(
				_projectId,
				"K1",
				Guid.NewGuid());

			Assert.Null(result.Value);

			var error = Assert.Single(result.Validation.Errors);

			Assert.Equal(nameof(ILocation.ParentId), error.PropertyName);
			Assert.Empty(_repository.Location.GetByProjectId(_projectId));
		}

		[Fact(DisplayName = "A location cannot use a parent belonging to another project")]
		public void Create_ParentFromAnotherProject_IsRejected()
		{
			var root = Add("AS1");

			var otherProject = _pdsx.Project.Create(
				"OTHER",
				_directory,
				"Project isolation test");

			Assert.True(otherProject.Validation.IsValid);

			var result = _repository.Location.Create(
				otherProject.Value!.Id,
				"K1",
				root.Id);

			Assert.Null(result.Value);

			var error = Assert.Single(result.Validation.Errors);

			Assert.Equal(nameof(ILocation.ParentId), error.PropertyName);
		}

		[Theory(DisplayName = "Invalid location designations are rejected")]
		[InlineData("")]
		[InlineData("   ")]
		public void Create_InvalidDesignation_IsRejected(string designation)
		{
			var result = _repository.Location.Create(
				_projectId,
				designation);

			Assert.Null(result.Value);

			var error = Assert.Single(result.Validation.Errors);

			Assert.Equal(nameof(ILocation.Designation), error.PropertyName);
			Assert.Empty(_repository.Location.GetByProjectId(_projectId));
		}

		[Fact(DisplayName =	"Renaming a location preserves its identity, depth and child relationships")]
		public void Update_Designation_PreservesStructure()
		{
			var root = Add("AS1");
			var child = Add("K1", root.Id);

			root = _repository.Location.GetById(root.Id);

			var previousVersion = root.LoadedVersion;

			root.Designation = "AS2";

			var result = _repository.Location.Update(root);

			Assert.True(result.Validation.IsValid);
			Assert.NotNull(result.Value);
			Assert.Equal(root.Id, result.Value!.Id);
			Assert.NotEqual(previousVersion, result.Value.LoadedVersion);

			var storedRoot = _repository.Location.GetById(root.Id)!;
			var storedChild = _repository.Location.GetById(child.Id)!;

			Assert.Equal("AS2", storedRoot.Designation);
			Assert.Null(storedRoot.ParentId);
			Assert.Equal(0, storedRoot.Depth);

			Assert.Equal(root.Id, storedChild.ParentId);
			Assert.Equal("K1", storedChild.Designation);
			Assert.Equal(1, storedChild.Depth);

			// The returned version must support another update.
			result.Value.Designation = "AS3";

			Assert.True(
				_repository.Location.Update(result.Value).Validation.IsValid);
		}

		[Fact(DisplayName =	"Renaming to an existing designation at the same depth is rejected")]
		public void Update_DuplicateDesignationAtSameDepth_IsRejected()
		{
			var firstRoot = Add("AS1");
			var secondRoot = Add("AS2");

			Add("K1", firstRoot.Id);
			var second = Add("K2", secondRoot.Id);

			second.Designation = "K1";

			var result = _repository.Location.Update(second);

			Assert.Null(result.Value);
			Assert.False(result.Validation.IsValid);

			var error = Assert.Single(result.Validation.Errors);

			Assert.Equal(nameof(ILocation.Designation), error.PropertyName);

			var stored = _repository.Location.GetById(second.Id)!;

			Assert.Equal("K2", stored.Designation);
			Assert.Equal(secondRoot.Id, stored.ParentId);
		}

		[Fact(DisplayName =	"Renaming to a designation used at another depth is allowed")]
		public void Update_SameDesignationAtDifferentDepth_IsAllowed()
		{
			var root = Add("AS1");
			Add("K1", root.Id);

			var intermediate = Add("B5", root.Id);
			var nested = Add("K2", intermediate.Id);

			nested.Designation = "K1";

			var result = _repository.Location.Update(nested);

			Assert.True(result.Validation.IsValid);
			Assert.Equal("K1", result.Value!.Designation);
			Assert.Equal(2, result.Value.Depth);
		}

		[Fact(DisplayName =	"Invalid location updates preserve the stored designation")]
		public void Update_InvalidDesignation_IsRejected()
		{
			var location = Add("AS1");
			location.Designation = "";

			var result = _repository.Location.Update(location);

			Assert.Null(result.Value);
			Assert.False(result.Validation.IsValid);

			Assert.Equal(
				"AS1",
				_repository.Location.GetById(location.Id)!.Designation);
		}

		[Fact(DisplayName =	"An outdated location cannot overwrite a newer designation")]
		public void Update_OutdatedVersion_IsRejected()
		{
			var created = Add("AS1");

			var first = _repository.Location.GetById(created.Id)!;
			var outdated = _repository.Location.GetById(created.Id)!;

			first.Designation = "AS2";

			Assert.True(_repository.Location.Update(first).Validation.IsValid);

			outdated.Designation = "AS3";

			var result = _repository.Location.Update(outdated);

			Assert.Null(result.Value);
			Assert.False(result.Validation.IsValid);

			Assert.Equal(
				"AS2",
				_repository.Location.GetById(created.Id)!.Designation);

			Assert.Equal("AS3", outdated.Designation);
		}

		[Fact(DisplayName =	"Updating a designation cannot silently move a location to another parent")]
		public void Update_ChangedParent_IsRejected()
		{
			var firstRoot = Add("AS1");
			var secondRoot = Add("AS2");
			var child = Add("K1", firstRoot.Id);

			child.ParentId = secondRoot.Id;

			var result = _repository.Location.Update(child);

			Assert.Null(result.Value);
			Assert.False(result.Validation.IsValid);

			var error = Assert.Single(result.Validation.Errors);

			Assert.Equal(nameof(ILocation.ParentId), error.PropertyName);
			Assert.Equal(
				firstRoot.Id,
				_repository.Location.GetById(child.Id)!.ParentId);
		}


		[Fact(DisplayName = "Deleting an unused leaf location preserves its parent")]
		public void Delete_UnusedLeaf_RemovesOnlySelectedNode()
		{
			var root = Add("AS1");
			var child = Add("K1", root.Id);

			var result = _repository.Location.Delete(child);

			Assert.True(result.IsValid);
			Assert.Null(_repository.Location.GetById(child.Id));
			Assert.NotNull(_repository.Location.GetById(root.Id));
			Assert.Empty(_repository.Location.GetChildren(root.Id));
		}

		[Fact(DisplayName =	"An unused root location can be deleted")]
		public void Delete_UnusedRoot_IsAllowed()
		{
			var root = Add("AS1");

			var result = _repository.Location.Delete(root);

			Assert.True(result.IsValid);
			Assert.Empty(_repository.Location.GetRoots(_projectId));
		}

		[Fact(DisplayName =	"A location containing children cannot be deleted")]
		public void Delete_NodeWithChildren_IsRejected()
		{
			var root = Add("AS1");
			var child = Add("K1", root.Id);

			var result = _repository.Location.Delete(root);

			Assert.False(result.IsValid);
			Assert.NotNull(_repository.Location.GetById(root.Id));
			Assert.NotNull(_repository.Location.GetById(child.Id));
		}

		[Fact(DisplayName =	"A location used by an engineering object cannot be deleted")]
		public void Delete_NodeWithObjectMembership_IsRejected()
		{
			var location = Add("AS1");
			var objectId = Guid.NewGuid();

			_database.Execute(unitOfWork =>
			{
				unitOfWork.Save(new EngineeringObject
				{
					Id = objectId,
					ProjectId = _projectId,
					LocationId = location.Id
				});

				unitOfWork.CommitChanges();
			});

			var result = _repository.Location.Delete(location);

			Assert.False(result.IsValid);
			Assert.NotNull(_repository.Location.GetById(location.Id));

			var storedObject = _database.Execute(unitOfWork =>
				unitOfWork.GetObjectByKey<EngineeringObject>(objectId));

			Assert.NotNull(storedObject);
			Assert.Equal(location.Id, storedObject!.LocationId);
		}

		[Fact(DisplayName =	"A location represented by an engineering object cannot be deleted")]
		public void Delete_RepresentedNode_IsRejected()
		{
			var membership = Add("AS1");
			var represented = Add("AS2");
			var objectId = Guid.NewGuid();

			_database.Execute(unitOfWork =>
			{
				unitOfWork.Save(new EngineeringObject
				{
					Id = objectId,
					ProjectId = _projectId,
					LocationId = membership.Id
				});

				var node = unitOfWork.GetObjectByKey<Location>(represented.Id);

				node.RepresentedByObjectId = objectId;

				unitOfWork.Save(node);
				unitOfWork.CommitChanges();
			});

			// Read the current version after assigning the representing object.
			var current = _repository.Location.GetById(represented.Id)!;

			var result = _repository.Location.Delete(current);

			Assert.False(result.IsValid);

			var error = Assert.Single(result.Errors);

			Assert.Equal(
				nameof(ILocation.RepresentedByObjectId),
				error.PropertyName);

			Assert.NotNull(_repository.Location.GetById(represented.Id));
		}

		[Fact(DisplayName =	"An outdated location cannot delete a subsequently updated node")]
		public void Delete_OutdatedVersion_IsRejected()
		{
			var original = Add("AS1");
			var current = _repository.Location.GetById(original.Id)!;

			current.Designation = "AS2";

			Assert.True(_repository.Location.Update(current).Validation.IsValid);

			var result = _repository.Location.Delete(original);

			Assert.False(result.IsValid);

			var stored = _repository.Location.GetById(original.Id);

			Assert.NotNull(stored);
			Assert.Equal("AS2", stored!.Designation);
		}

		[Fact(DisplayName =	"Deleting an already deleted location returns an identifier validation error")]
		public void Delete_AlreadyDeletedNode_ReturnsFailure()
		{
			var location = Add("AS1");

			Assert.True(_repository.Location.Delete(location).IsValid);

			var result = _repository.Location.Delete(location);

			Assert.False(result.IsValid);

			var error = Assert.Single(result.Errors);

			Assert.Equal(nameof(ILocation.Id), error.PropertyName);
		}

		[Fact(DisplayName = "An object can represent its location node while retaining its membership")]
		public void SetRepresentingObject_AssignsReference()
		{
			var location = Add("AS1");
			var engineeringObject = AddObject(location.Id);

			// Object creation changed the location's dependency version.
			var current = _repository.Location.GetById(location.Id)!;

			var result = _repository.Location.SetRepresentingObject(
				current,
				engineeringObject.Id);

			Assert.True(result.Validation.IsValid);
			Assert.NotNull(result.Value);
			Assert.Equal(
				engineeringObject.Id,
				result.Value!.RepresentedByObjectId);

			var storedLocation = _repository.Location.GetById(location.Id)!;
			var storedObject =
				_repository.EngineeringObject.GetById(engineeringObject.Id)!;

			Assert.Equal(
				engineeringObject.Id,
				storedLocation.RepresentedByObjectId);

			Assert.Equal(location.Id, storedObject.LocationId);
			Assert.Null(storedObject.SheetId);
		}

		[Fact(DisplayName =	"Assigning a representing object does not move its existing aspect membership")]
		public void SetRepresentingObject_PreservesObjectMembership()
		{
			var membership = Add("AS1");
			var represented = Add("AS2");
			var engineeringObject = AddObject(membership.Id);

			var result = _repository.Location.SetRepresentingObject(
				represented,
				engineeringObject.Id);

			Assert.True(result.Validation.IsValid);

			Assert.Equal(
				engineeringObject.Id,
				_repository.Location.GetById(represented.Id)!.RepresentedByObjectId);

			Assert.Equal(
				membership.Id,
				_repository.EngineeringObject.GetById(engineeringObject.Id)!.LocationId);
		}

		[Fact(DisplayName =	"Clearing representation preserves the object and its aspect membership")]
		public void SetRepresentingObject_ClearReference_PreservesObject()
		{
			var location = Add("AS1");
			var engineeringObject = AddObject(location.Id);

			var current = _repository.Location.GetById(location.Id)!;

			var assigned = _repository.Location.SetRepresentingObject(
				current,
				engineeringObject.Id);

			Assert.True(assigned.Validation.IsValid);

			var cleared = _repository.Location.SetRepresentingObject(
				assigned.Value!,
				null);

			Assert.True(cleared.Validation.IsValid);
			Assert.Null(cleared.Value!.RepresentedByObjectId);

			Assert.Null(
				_repository.Location.GetById(location.Id)!.RepresentedByObjectId);

			var storedObject =
				_repository.EngineeringObject.GetById(engineeringObject.Id);

			Assert.NotNull(storedObject);
			Assert.Equal(location.Id, storedObject!.LocationId);
		}

		[Fact(DisplayName =	"A missing object cannot represent a location node")]
		public void SetRepresentingObject_MissingObject_IsRejected()
		{
			var location = Add("AS1");

			var result = _repository.Location.SetRepresentingObject(
				location,
				Guid.NewGuid());

			Assert.Null(result.Value);

			var error = Assert.Single(result.Validation.Errors);

			Assert.Equal(
				nameof(ILocation.RepresentedByObjectId),
				error.PropertyName);

			var stored = _repository.Location.GetById(location.Id)!;

			Assert.Null(stored.RepresentedByObjectId);
			Assert.Equal(location.LoadedVersion, stored.LoadedVersion);
		}

		[Fact(DisplayName =	"An object from another project cannot represent a location node")]
		public void SetRepresentingObject_OtherProject_IsRejected()
		{
			var location = Add("AS1");

			var otherProject = _pdsx.Project.Create(
				"OTHER",
				_directory,
				"Representation boundary test");

			Assert.True(otherProject.Validation.IsValid);

			var otherLocation = _repository.Location.Create(
				otherProject.Value!.Id,
				"AS1");

			Assert.True(otherLocation.Validation.IsValid);

			var input = _repository.EngineeringObject.New(otherProject.Value.Id);
			input.LocationId = otherLocation.Value!.Id;

			var otherObject = _repository.EngineeringObject.Create(input);

			Assert.True(otherObject.Validation.IsValid);

			var result = _repository.Location.SetRepresentingObject(
				location,
				otherObject.Value!.Id);

			Assert.Null(result.Value);
			Assert.False(result.Validation.IsValid);

			Assert.Null(
				_repository.Location.GetById(location.Id)!.RepresentedByObjectId);
		}

		[Fact(DisplayName =	"An outdated location cannot overwrite a newer representation")]
		public void SetRepresentingObject_OutdatedVersion_IsRejected()
		{
			var membership = Add("AS1");
			var represented = Add("AS2");

			var firstObject = AddObject(membership.Id);
			var secondObject = AddObject(membership.Id);

			var current = _repository.Location.GetById(represented.Id)!;
			var outdated = _repository.Location.GetById(represented.Id)!;

			var assigned = _repository.Location.SetRepresentingObject(
				current,
				firstObject.Id);

			Assert.True(assigned.Validation.IsValid);

			var rejected = _repository.Location.SetRepresentingObject(
				outdated,
				secondObject.Id);

			Assert.Null(rejected.Value);
			Assert.False(rejected.Validation.IsValid);

			Assert.Equal(
				firstObject.Id,
				_repository.Location.GetById(represented.Id)!.RepresentedByObjectId);
		}

		[Fact(DisplayName = "Creating a child advances the parent's dependency version")]
		public void Create_Child_UpdatesParentVersion()
		{
			var parent = Add("AS1");
			var originalVersion = parent.LoadedVersion;

			var child = Add("K1", parent.Id);

			var storedParent = _repository.Location.GetById(parent.Id)!;

			Assert.NotEqual(originalVersion, storedParent.LoadedVersion);
			Assert.Equal(parent.Id, child.ParentId);
			Assert.Equal(1, child.Depth);
		}

		[Fact(DisplayName =	"A child is not persisted when its parent is deleted after reference validation")]
		public void Create_ParentDeletedBeforeCommit_DoesNotLeaveOrphan()
		{
			var parent = Add("AS1");

			var competingCreate = new Create(
				_database,
				beforePersist: () =>
				{
					// The new child is still uncommitted in the other unit of work.
					var deleted = _repository.Location.Delete(parent);

					Assert.True(deleted.IsValid);
				});

			var locations = new LocationRepository(
				new Read(_database),
				competingCreate,
				new Update(_database),
				new Delete(_database),
				new MoveLocation(_database),
				new LocationChangeWriter(_database));

			var result = locations.Create(
				_projectId,
				"K1",
				parent.Id);

			Assert.Null(result.Value);
			Assert.False(result.Validation.IsValid);

			Assert.Null(_repository.Location.GetById(parent.Id));
			Assert.Empty(_repository.Location.GetChildren(parent.Id));
			Assert.Empty(_repository.Location.GetByProjectId(_projectId));
		}

		[Fact(DisplayName =	"A parent deletion is rejected when a child is committed after dependency validation")]
		public void Delete_ChildCreatedBeforeCommit_PreservesParentAndChild()
		{
			var parent = Add("AS1");
			var delete = new Delete(_database);

			ILocation? createdChild = null;

			var result = delete.Remove<Location>(
				parent.Id,
				parent.LoadedVersion,
				beforeDelete: (unitOfWork, existing) =>
				{
					// A separate operation commits a child after the deletion checks.
					var created = _repository.Location.Create(
						_projectId,
						"K1",
						existing.Id);

					Assert.True(created.Validation.IsValid);
					createdChild = created.Value;
				},
				validateDatabase: (unitOfWork, existing) =>
				{
					var hasChildren = new XPQuery<Location>(unitOfWork)
						.Any(item => item.ParentId == existing.Id);

					Assert.False(hasChildren);

					return new ValidationResult();
				});

			Assert.False(result.IsValid);
			Assert.NotNull(createdChild);

			Assert.NotNull(_repository.Location.GetById(parent.Id));

			var storedChild =
				_repository.Location.GetById(createdChild!.Id);

			Assert.NotNull(storedChild);
			Assert.Equal(parent.Id, storedChild!.ParentId);
		}

		#region Move

		[Fact(DisplayName =	"Moving a location preserves identifiers and updates all subtree depths")]
		public void Move_Subtree_PreservesIdentifiersAndUpdatesDepths()
		{
			var source = Add("AS1");
			var child = Add("K1", source.Id);
			var grandchild = Add("B1", child.Id);
			var destination = Add("AS2");

			// Adding children changes the parent's version.
			source = _repository.Location.GetById(source.Id)!;

			var result = _repository.Location.Move(source, destination.Id);

			Assert.True(result.Validation.IsValid);
			Assert.NotNull(result.Value);
			Assert.Equal(source.Id, result.Value!.Id);
			Assert.Equal(destination.Id, result.Value.ParentId);
			Assert.Equal(1, result.Value.Depth);

			var storedChild = _repository.Location.GetById(child.Id);
			var storedGrandchild = _repository.Location.GetById(grandchild.Id);

			Assert.NotNull(storedChild);
			Assert.Equal(source.Id, storedChild!.ParentId);
			Assert.Equal(2, storedChild.Depth);

			Assert.NotNull(storedGrandchild);
			Assert.Equal(child.Id, storedGrandchild!.ParentId);
			Assert.Equal(3, storedGrandchild.Depth);
		}

		[Fact(DisplayName =	"A location and its descendants can be moved to the root level")]
		public void Move_ToRoot_UpdatesDepths()
		{
			var parent = Add("AS1");
			var source = Add("B1", parent.Id);
			var child = Add("K1", source.Id);

			source = _repository.Location.GetById(source.Id)!;

			var result = _repository.Location.Move(source, null);

			Assert.True(result.Validation.IsValid);
			Assert.NotNull(result.Value);
			Assert.Null(result.Value!.ParentId);
			Assert.Equal(0, result.Value.Depth);

			var storedChild = _repository.Location.GetById(child.Id)!;

			Assert.Equal(source.Id, storedChild.ParentId);
			Assert.Equal(1, storedChild.Depth);
		}

		[Fact(DisplayName = "A location cannot be moved beneath itself or a descendant")]
		public void Move_ToSelfOrDescendant_IsRejected()
		{
			var source = Add("AS1");
			var child = Add("B1", source.Id);

			source = _repository.Location.GetById(source.Id)!;

			var selfResult = _repository.Location.Move(source, source.Id);
			var descendantResult = _repository.Location.Move(source, child.Id);

			Assert.False(selfResult.Validation.IsValid);
			Assert.False(descendantResult.Validation.IsValid);

			var stored = _repository.Location.GetById(source.Id)!;

			Assert.Null(stored.ParentId);
			Assert.Equal(0, stored.Depth);
		}

		[Fact(DisplayName =	"A move is rejected when the final depth would contain a duplicate designation")]
		public void Move_DuplicateAtFinalDepth_IsRejected()
		{
			var source = Add("K1");
			var destination = Add("AS1");
			Add("K1", destination.Id);

			var result = _repository.Location.Move(source, destination.Id);

			Assert.False(result.Validation.IsValid);
			Assert.Null(result.Value);

			var stored = _repository.Location.GetById(source.Id)!;

			Assert.Null(stored.ParentId);
			Assert.Equal(0, stored.Depth);
			Assert.Equal(source.LoadedVersion, stored.LoadedVersion);
		}

		[Fact(DisplayName =	"A valid move succeeds when intermediate depths would otherwise collide")]
		public void Move_OverlappingIntermediateDepths_Succeeds()
		{
			// The same designation is allowed at different depths.
			var source = Add("K1");
			var child = Add("K1", source.Id);
			var destination = Add("AS1");

			source = _repository.Location.GetById(source.Id)!;

			var result = _repository.Location.Move(source, destination.Id);

			Assert.True(result.Validation.IsValid);
			Assert.Equal(1, result.Value!.Depth);

			var storedChild = _repository.Location.GetById(child.Id)!;

			Assert.Equal(2, storedChild.Depth);
			Assert.Equal(source.Id, storedChild.ParentId);
		}

		[Fact(DisplayName = "Failure after staging rolls back every subtree change")]
		public void Move_FailureAfterStaging_RollsBack()
		{
			var source = Add("AS1");
			var child = Add("K1", source.Id);
			var destination = Add("AS2");

			source = _repository.Location.GetById(source.Id)!;
			child = _repository.Location.GetById(child.Id)!;

			var mover = new MoveLocation(
				_database,
				afterStaging: () =>
					throw new InvalidOperationException("Simulated failure."));

			Assert.Throws<InvalidOperationException>(() =>
			{
				mover.Execute(source, destination.Id);
			});

			var storedSource = _repository.Location.GetById(source.Id)!;
			var storedChild = _repository.Location.GetById(child.Id)!;

			Assert.Null(storedSource.ParentId);
			Assert.Equal(0, storedSource.Depth);
			Assert.Equal(source.LoadedVersion, storedSource.LoadedVersion);

			Assert.Equal(source.Id, storedChild.ParentId);
			Assert.Equal(1, storedChild.Depth);
			Assert.Equal(child.LoadedVersion, storedChild.LoadedVersion);
		}

		[Fact(DisplayName = "A stale location cannot be moved")]
		public void Move_StaleVersion_IsRejected()
		{
			var source = Add("AS1");
			var destination = Add("AS2");

			// Creating a child changes the source version.
			Add("K1", source.Id);

			var result = _repository.Location.Move(source, destination.Id);

			Assert.False(result.Validation.IsValid);
			Assert.Null(result.Value);

			Assert.Null(
				_repository.Location.GetById(source.Id)!.ParentId);
		}

		[Fact(DisplayName = "Moving to the current parent does not change the location version")]
		public void Move_UnchangedParent_DoesNotWrite()
		{
			var parent = Add("AS1");
			var source = Add("K1", parent.Id);

			var result = _repository.Location.Move(source, parent.Id);

			Assert.True(result.Validation.IsValid);
			Assert.Equal(source.LoadedVersion, result.Value!.LoadedVersion);

			var stored = _repository.Location.GetById(source.Id)!;

			Assert.Equal(source.LoadedVersion, stored.LoadedVersion);
		}

		[Fact(DisplayName = "Renaming a parent preserves its children and their identifiers")]
		public void Change_RenameParent_PreservesChildren()
		{
			var root = Add("AS1");
			var parent = Add("B3", root.Id);
			var first = Add("Q02", parent.Id);
			var second = Add("Q03", parent.Id);

			parent = _repository.Location.GetById(parent.Id)!;

			var writer = new LocationChangeWriter(_database);

			var result = writer.Apply(parent, new[] { "AS1", "B4" });

			Assert.True(result.Validation.IsValid);
			Assert.Equal(parent.Id, result.Value!.Id);
			Assert.Equal("B4", result.Value.Designation);

			Assert.Equal(
				parent.Id,
				_repository.Location.GetById(first.Id)!.ParentId);

			Assert.Equal(
				parent.Id,
				_repository.Location.GetById(second.Id)!.ParentId);
		}

		[Fact(DisplayName =	"Moving one child creates the target parent and preserves its sibling")]
		public void Change_MoveOneChild_PreservesSibling()
		{
			var root = Add("AS1");
			var parent = Add("B3", root.Id);
			var first = Add("Q02", parent.Id);
			var second = Add("Q03", parent.Id);
			var entity = AddObject(first.Id);

			first = _repository.Location.GetById(first.Id)!;

			var writer = new LocationChangeWriter(_database);

			var result = writer.Apply(
				first,
				new[] { "AS1", "B4", "Q02" });

			Assert.True(result.Validation.IsValid);
			Assert.Equal(first.Id, result.Value!.Id);

			var targetParent = _repository.Location
				.GetById(result.Value.ParentId!.Value)!;

			Assert.Equal("B4", targetParent.Designation);
			Assert.Equal(root.Id, targetParent.ParentId);

			Assert.NotNull(_repository.Location.GetById(parent.Id));
			Assert.Equal(
				parent.Id,
				_repository.Location.GetById(second.Id)!.ParentId);

			var storedObject = _repository.EngineeringObject.GetById(entity.Id)!;
			Assert.Equal(first.Id, storedObject.LocationId);
		}

		[Fact(DisplayName =	"Moving the last child removes its unused previous parent")]
		public void Change_MoveLastChild_RemovesUnusedParent()
		{
			var root = Add("AS1");
			var parent = Add("B3", root.Id);
			var child = Add("Q02", parent.Id);

			var writer = new LocationChangeWriter(_database);

			var result = writer.Apply(
				child,
				new[] { "AS1", "B4", "Q02" });

			Assert.True(result.Validation.IsValid);
			Assert.Null(_repository.Location.GetById(parent.Id));
			Assert.NotNull(_repository.Location.GetById(root.Id));
			Assert.Equal(child.Id, result.Value!.Id);
		}

		[Fact(DisplayName =	"An empty previous parent is preserved while an object belongs to it")]
		public void Change_PreviousParentWithObject_IsPreserved()
		{
			var root = Add("AS1");
			var parent = Add("B3", root.Id);
			var child = Add("Q02", parent.Id);

			AddObject(parent.Id);

			var writer = new LocationChangeWriter(_database);

			var result = writer.Apply(
				child,
				new[] { "AS1", "B4", "Q02" });

			Assert.True(result.Validation.IsValid);
			Assert.NotNull(_repository.Location.GetById(parent.Id));
		}

		[Fact(DisplayName =	"A conflicting rename leaves the existing hierarchy unchanged")]
		public void Change_ConflictingRename_IsRejected()
		{
			var root = Add("AS1");
			var parent = Add("B3", root.Id);
			var source = Add("Q02", parent.Id);
			var other = Add("Q03", parent.Id);

			var writer = new LocationChangeWriter(_database);

			var result = writer.Apply(
				source,
				new[] { "AS1", "B3", "Q03" });

			Assert.False(result.Validation.IsValid);
			Assert.Null(result.Value);

			var stored = _repository.Location.GetById(source.Id)!;

			Assert.Equal("Q02", stored.Designation);
			Assert.Equal(parent.Id, stored.ParentId);
			Assert.NotNull(_repository.Location.GetById(other.Id));
		}

		[Fact(DisplayName =	"A failure after staging rolls back the entire location change")]
		public void Change_FailureAfterStaging_RollsBack()
		{
			var root = Add("AS1");
			var parent = Add("B3", root.Id);
			var child = Add("Q02", parent.Id);

			var originalVersion = child.LoadedVersion;

			var writer = new LocationChangeWriter(
				_database,
				afterStaging: () =>
					throw new InvalidOperationException("Simulated failure."));

			Assert.Throws<InvalidOperationException>(() =>
			{
				writer.Apply(child, new[] { "AS1", "B4", "Q02" });
			});

			var stored = _repository.Location.GetById(child.Id)!;

			Assert.Equal("Q02", stored.Designation);
			Assert.Equal(parent.Id, stored.ParentId);
			Assert.Equal(2, stored.Depth);
			Assert.Equal(originalVersion, stored.LoadedVersion);

			Assert.NotNull(_repository.Location.GetById(parent.Id));

			Assert.DoesNotContain(
				_repository.Location.GetByProjectId(_projectId),
				location => location.Designation == "B4");
		}

		[Fact(DisplayName =	"A stale location cannot change its designation path")]
		public void Change_StaleVersion_IsRejected()
		{
			var root = Add("AS1");
			var parent = Add("B3", root.Id);
			var child = Add("Q02", parent.Id);

			// Object creation changes the location's dependency version.
			AddObject(child.Id);

			var writer = new LocationChangeWriter(_database);

			var result = writer.Apply(
				child,
				new[] { "AS1", "B4", "Q02" });

			Assert.False(result.Validation.IsValid);
			Assert.Null(result.Value);

			Assert.Equal(
				parent.Id,
				_repository.Location.GetById(child.Id)!.ParentId);
		}

		[Fact(DisplayName = "The location repository changes a path and preserves object identity")]
		public void ChangePath_ThroughRepository_PreservesObjectIdentity()
		{
			var root = Add("AS1");
			var previousParent = Add("B3", root.Id);
			var location = Add("Q02", previousParent.Id);
			var entity = AddObject(location.Id);

			// Object creation changes the location's dependency version.
			location = _repository.Location.GetById(location.Id)!;

			var result = _repository.Location.ChangePath(
				location,
				new[] { "AS1", "B4", "Q02" });

			Assert.True(result.Validation.IsValid);
			Assert.NotNull(result.Value);
			Assert.Equal(location.Id, result.Value!.Id);

			var newParent = _repository.Location.GetById(
				result.Value.ParentId!.Value);

			Assert.NotNull(newParent);
			Assert.Equal("B4", newParent!.Designation);
			Assert.Equal(root.Id, newParent.ParentId);

			Assert.Null(
				_repository.Location.GetById(previousParent.Id));

			var storedObject = _repository.EngineeringObject.GetById(entity.Id);

			Assert.NotNull(storedObject);
			Assert.Equal(entity.Id, storedObject!.Id);
			Assert.Equal(location.Id, storedObject.LocationId);
		}

		#endregion Move

		public void Dispose()
		{
			if (Directory.Exists(_directory))
				Directory.Delete(_directory, recursive: true);
		}
	}
}