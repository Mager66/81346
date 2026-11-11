using System;
using System.IO;
using ELPROCAD.DataAccess.Contracts.MDBX.Documents;
using ELPROCAD.DataAccess.Contracts.MDBX.Objects;
using ELPROCAD.DataAccess.Repositories.MDBX;
using FluentValidation.Results;
using Xunit;

namespace ELPROCAD.DataAccess.Tests.Database
{
	public sealed class CadSymbolLinkRepositoryTests : IDisposable
	{
		private readonly string _directory = Path.Combine(
			Path.GetTempPath(),
			"ELPROCAD.DataAccess.Tests",
			Guid.NewGuid().ToString("N"));

		private readonly IMdbxRepository _mdbx;
		private readonly Guid _projectId;
		private readonly Guid _fileFormatId;
		private readonly Guid _locationId;

		public CadSymbolLinkRepositoryTests()
		{
			IDataAccessInitializer initializer = new DataAccessInitializer();

			var pdsx = initializer.InitializePdsx(
				Path.Combine(_directory, "Project.pdsx"));

			var project = Require(pdsx.Project.Create(
				"Symbol link tests",
				_directory,
				"CAD symbol link integration tests"));

			_projectId = project.Id;
			_fileFormatId = pdsx.FileFormat.GetByExtension(".dwg")!.Id;

			_mdbx = initializer.InitializeMdbx(
				Path.Combine(_directory, "Project.mdbx"));

			_locationId = Require(
				_mdbx.Location.Create(_projectId, "AS1")).Id;
		}

		private IDocument AddDocument(Guid? projectId = null)
		{
			var name = Guid.NewGuid().ToString("N") + ".dwg";

			return Require(_mdbx.Document.Create(
				projectId ?? _projectId,
				_fileFormatId,
				name,
				Path.Combine(_directory, name)));
		}

		private IEngineeringObject AddObject()
		{
			var entity = _mdbx.EngineeringObject.New(_projectId);
			entity.LocationId = _locationId;

			return Require(_mdbx.EngineeringObject.Create(entity));
		}

		private static T Require<T>(
			(T? Value, ValidationResult Validation) result)
			where T : class
		{
			Assert.True(result.Validation.IsValid);
			Assert.NotNull(result.Value);
			return result.Value!;
		}

		[Fact(DisplayName =	"A registered symbol can be retrieved by document and block handle")]
		public void Create_GetByBlock_ReturnsLink()
		{
			var document = AddDocument();
			var entity = AddObject();

			var created = Require(_mdbx.CadSymbolLink.Create(
				document.Id, 0x63F, entity.Id));

			var stored = _mdbx.CadSymbolLink.GetByBlock(
				document.Id, 0x63F);

			Assert.NotNull(stored);
			Assert.Equal(created.Id, stored!.Id);
			Assert.Equal(entity.Id, stored.EngineeringObjectId);
			Assert.NotNull(stored.LoadedVersion);
		}

		[Fact(DisplayName =	"An existing symbol link cannot be replaced by another object")]
		public void Create_ExistingBlock_IsRejected()
		{
			var document = AddDocument();
			var original = AddObject();
			var other = AddObject();

			Require(_mdbx.CadSymbolLink.Create(
				document.Id, 0x63F, original.Id));

			var result = _mdbx.CadSymbolLink.Create(
				document.Id, 0x63F, other.Id);

			Assert.False(result.Validation.IsValid);
			Assert.Null(result.Value);

			Assert.Equal(
				original.Id,
				_mdbx.CadSymbolLink
					.GetByBlock(document.Id, 0x63F)!
					.EngineeringObjectId);

			Assert.Single(
				_mdbx.CadSymbolLink.GetByDocumentId(document.Id));
		}

		[Fact(DisplayName =	"A symbol link requires an existing document")]
		public void Create_MissingDocument_IsRejected()
		{
			var entity = AddObject();

			var result = _mdbx.CadSymbolLink.Create(
				Guid.NewGuid(), 0x63F, entity.Id);

			Assert.False(result.Validation.IsValid);
			Assert.Null(result.Value);
			Assert.Empty(_mdbx.CadSymbolLink.GetByObjectId(entity.Id));
		}

		[Fact(DisplayName =	"A symbol link requires an existing engineering object")]
		public void Create_MissingObject_IsRejected()
		{
			var document = AddDocument();

			var result = _mdbx.CadSymbolLink.Create(
				document.Id, 0x63F, Guid.NewGuid());

			Assert.False(result.Validation.IsValid);
			Assert.Null(result.Value);
			Assert.Empty(_mdbx.CadSymbolLink.GetByDocumentId(document.Id));
		}

		[Fact(DisplayName =	"A symbol cannot reference an object from another project")]
		public void Create_DifferentProjects_IsRejected()
		{
			// Documents store the external project identifier as a scalar.
			var document = AddDocument(Guid.NewGuid());
			var entity = AddObject();

			var result = _mdbx.CadSymbolLink.Create(
				document.Id, 0x63F, entity.Id);

			Assert.False(result.Validation.IsValid);
			Assert.Null(result.Value);
			Assert.Empty(_mdbx.CadSymbolLink.GetByDocumentId(document.Id));
		}

		[Fact(DisplayName =	"Multiple symbols can be registered for the same object")]
		public void Create_MultipleSymbolsForObject_IsAllowed()
		{
			var document = AddDocument();
			var entity = AddObject();

			Require(_mdbx.CadSymbolLink.Create(
				document.Id, 0x63F, entity.Id));

			Require(_mdbx.CadSymbolLink.Create(
				document.Id, 0x640, entity.Id));

			Assert.Equal(
				2,
				_mdbx.CadSymbolLink.GetByObjectId(entity.Id).Count);
		}

		[Fact(DisplayName =	"A document with registered symbols cannot be deleted")]
		public void DeleteDocument_WithSymbolLink_IsRejected()
		{
			var document = AddDocument();
			var entity = AddObject();

			Require(_mdbx.CadSymbolLink.Create(
				document.Id, 0x63F, entity.Id));

			// Link creation changes the document's dependency version.
			document = _mdbx.Document.GetById(document.Id)!;

			var result = _mdbx.Document.Delete(document);

			Assert.False(result.IsValid);
			Assert.Contains(
				result.Errors,
				error => error.ErrorMessage ==
					"The document cannot be deleted while CAD symbols reference it.");

			Assert.NotNull(_mdbx.Document.GetById(document.Id));
		}

		[Fact(DisplayName =	"Deleting an object preserves its registered CAD symbols")]
		public void DeleteObject_WithSymbolLink_PreservesLink()
		{
			var document = AddDocument();
			var entity = AddObject();

			var link = Require(_mdbx.CadSymbolLink.Create(
				document.Id,
				0x63F,
				entity.Id));

			entity = _mdbx.EngineeringObject.GetById(entity.Id)!;

			Assert.True(_mdbx.EngineeringObject.Delete(entity).IsValid);

			var storedObject = _mdbx.EngineeringObject.GetById(entity.Id)!;
			var storedLink = _mdbx.CadSymbolLink.GetByBlock(document.Id, 0x63F);

			Assert.True(storedObject.IsMarkedForDeletion);
			Assert.NotNull(storedLink);
			Assert.Equal(link.Id, storedLink!.Id);
			Assert.Equal(entity.Id, storedLink.EngineeringObjectId);
		}

		[Fact(DisplayName =	"Restoring an object preserves its identity and existing CAD symbol link")]
		public void RestoreObject_WithSymbolLink_PreservesIdentityAndLink()
		{
			var document = AddDocument();
			var entity = AddObject();

			var createdLink = _mdbx.CadSymbolLink.Create(
				document.Id,
				0x542,
				entity.Id);

			Assert.True(createdLink.Validation.IsValid);
			Assert.NotNull(createdLink.Value);

			// Creating a symbol link changes the object's dependency version.
			var current = _mdbx.EngineeringObject.GetById(entity.Id)!;

			var deleted = _mdbx.EngineeringObject.Delete(current);

			Assert.True(deleted.IsValid);

			var marked = _mdbx.EngineeringObject.GetById(entity.Id)!;

			Assert.True(marked.IsMarkedForDeletion);
			Assert.NotNull(marked.DeletedAtUtc);

			var restored = _mdbx.EngineeringObject.Restore(marked);

			Assert.True(restored.Validation.IsValid);
			Assert.NotNull(restored.Value);
			Assert.Equal(entity.Id, restored.Value!.Id);
			Assert.False(restored.Value.IsMarkedForDeletion);
			Assert.Null(restored.Value.DeletedAtUtc);

			// Verify the persisted state through a new database operation.
			var stored = _mdbx.EngineeringObject.GetById(entity.Id)!;
			var link = _mdbx.CadSymbolLink.GetByBlock(
				document.Id,
				0x542);

			Assert.False(stored.IsMarkedForDeletion);
			Assert.Null(stored.DeletedAtUtc);
			Assert.Equal(entity.LocationId, stored.LocationId);

			Assert.NotNull(link);
			Assert.Equal(createdLink.Value!.Id, link!.Id);
			Assert.Equal(entity.Id, link.EngineeringObjectId);
		}

		[Fact(DisplayName =	"A new CAD symbol link cannot reference an object marked for deletion")]
		public void CreateLink_MarkedObject_IsRejected()
		{
			var document = AddDocument();
			var entity = AddObject();

			var deleted = _mdbx.EngineeringObject.Delete(entity);

			Assert.True(deleted.IsValid);

			var result = _mdbx.CadSymbolLink.Create(
				document.Id,
				0x543,
				entity.Id);

			Assert.False(result.Validation.IsValid);
			Assert.Null(result.Value);

			Assert.Null(
				_mdbx.CadSymbolLink.GetByBlock(document.Id, 0x543));

			// A rejected link must not restore the object.
			var stored = _mdbx.EngineeringObject.GetById(entity.Id)!;

			Assert.True(stored.IsMarkedForDeletion);
		}

		[Fact(DisplayName =	"Deleting a CAD symbol link preserves its engineering object and document")]
		public void DeleteLink_PreservesObjectAndDocument()
		{
			var document = AddDocument();
			var entity = AddObject();

			var created = _mdbx.CadSymbolLink.Create(
				document.Id,
				0x542,
				entity.Id);

			Assert.True(created.Validation.IsValid);
			Assert.NotNull(created.Value);

			var result = _mdbx.CadSymbolLink.Delete(created.Value!);

			Assert.True(result.IsValid);
			Assert.Null(
				_mdbx.CadSymbolLink.GetByBlock(document.Id, 0x542));

			var stored = _mdbx.EngineeringObject.GetById(entity.Id);

			Assert.NotNull(stored);
			Assert.False(stored!.IsMarkedForDeletion);
			Assert.Equal(entity.LocationId, stored.LocationId);
			Assert.NotNull(_mdbx.Document.GetById(document.Id));
		}

		[Fact(DisplayName =	"Deleting one CAD symbol link preserves other links to the same object")]
		public void DeleteLink_PreservesOtherLinks()
		{
			var document = AddDocument();
			var entity = AddObject();

			var first = _mdbx.CadSymbolLink.Create(
				document.Id,
				0x542,
				entity.Id);

			var second = _mdbx.CadSymbolLink.Create(
				document.Id,
				0x543,
				entity.Id);

			Assert.True(first.Validation.IsValid);
			Assert.True(second.Validation.IsValid);

			// Reload the link before deleting it.
			var link = _mdbx.CadSymbolLink.GetByBlock(
				document.Id,
				0x542);

			Assert.NotNull(link);

			var result = _mdbx.CadSymbolLink.Delete(link!);

			Assert.True(result.IsValid);

			var remaining = Assert.Single(
				_mdbx.CadSymbolLink.GetByObjectId(entity.Id));

			Assert.Equal(second.Value!.Id, remaining.Id);
			Assert.Equal(0x543L, remaining.BlockHandle);
			Assert.NotNull(_mdbx.EngineeringObject.GetById(entity.Id));
		}

		public void Dispose()
		{
			if (Directory.Exists(_directory))
				Directory.Delete(_directory, recursive: true);
		}
	}
}