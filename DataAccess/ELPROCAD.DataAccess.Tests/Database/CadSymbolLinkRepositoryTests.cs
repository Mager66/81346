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

		[Fact(DisplayName =
			"A registered symbol can be retrieved by document and block handle")]
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

		[Fact(DisplayName =
			"An existing symbol link cannot be replaced by another object")]
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

		[Fact(DisplayName =
			"A symbol link requires an existing document")]
		public void Create_MissingDocument_IsRejected()
		{
			var entity = AddObject();

			var result = _mdbx.CadSymbolLink.Create(
				Guid.NewGuid(), 0x63F, entity.Id);

			Assert.False(result.Validation.IsValid);
			Assert.Null(result.Value);
			Assert.Empty(_mdbx.CadSymbolLink.GetByObjectId(entity.Id));
		}

		[Fact(DisplayName =
			"A symbol link requires an existing engineering object")]
		public void Create_MissingObject_IsRejected()
		{
			var document = AddDocument();

			var result = _mdbx.CadSymbolLink.Create(
				document.Id, 0x63F, Guid.NewGuid());

			Assert.False(result.Validation.IsValid);
			Assert.Null(result.Value);
			Assert.Empty(_mdbx.CadSymbolLink.GetByDocumentId(document.Id));
		}

		[Fact(DisplayName =
			"A symbol cannot reference an object from another project")]
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

		[Fact(DisplayName =
			"Multiple symbols can be registered for the same object")]
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

		[Fact(DisplayName =
			"A document with registered symbols cannot be deleted")]
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

		[Fact(DisplayName =
			"An object with registered symbols cannot be deleted")]
		public void DeleteObject_WithSymbolLink_IsRejected()
		{
			var document = AddDocument();
			var entity = AddObject();

			Require(_mdbx.CadSymbolLink.Create(
				document.Id, 0x63F, entity.Id));

			// Link creation changes the object's dependency version.
			entity = _mdbx.EngineeringObject.GetById(entity.Id)!;

			var result = _mdbx.EngineeringObject.Delete(entity);

			Assert.False(result.IsValid);
			Assert.Contains(
				result.Errors,
				error => error.ErrorMessage ==
					"The engineering object cannot be deleted while CAD symbols reference it.");

			Assert.NotNull(_mdbx.EngineeringObject.GetById(entity.Id));
		}

		public void Dispose()
		{
			if (Directory.Exists(_directory))
				Directory.Delete(_directory, recursive: true);
		}
	}
}