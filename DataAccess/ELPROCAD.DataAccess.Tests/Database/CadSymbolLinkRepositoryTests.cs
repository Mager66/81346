using ELPROCAD.DataAccess.Contracts.MDBX.Documents;
using ELPROCAD.DataAccess.Contracts.MDBX.Objects;
using ELPROCAD.DataAccess.Repositories.MDBX;
using FluentValidation.Results;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace ELPROCAD.DataAccess.Tests.Database
{
	public sealed class CadSymbolLinkRepositoryTests : IDisposable
	{
		private readonly string _directory;
		private readonly IMdbxRepository _repository;
		private readonly Guid _projectId;
		private readonly Guid _fileFormatId;
		private readonly Guid _locationId;

		public CadSymbolLinkRepositoryTests()
		{
			_directory = Path.Combine(
				Path.GetTempPath(),
				"ELPROCAD.DataAccess.Tests",
				Guid.NewGuid().ToString("N"));

			Directory.CreateDirectory(_directory);

			var initializer = new DataAccessInitializer();

			var pdsx = initializer.InitializePdsx(
				Path.Combine(_directory, "Project.pdsx"));

			var project = Require(pdsx.Project.Create(
				"TEST-81346",
				_directory,
				"CAD symbol link tests"));

			_projectId = project.Id;

			var format = pdsx.FileFormat.GetByExtension(".dwg");

			Assert.NotNull(format);
			_fileFormatId = format!.Id;

			_repository = initializer.InitializeMdbx(
				Path.Combine(_directory, "Project.mdbx"));

			var location = Require(
				_repository.Location.Create(_projectId, "AS1"));

			_locationId = location.Id;
		}

		[Fact(DisplayName =	"A CAD symbol link persists its document, sheet and object references")]
		public void Create_ValidLink_PersistsReferences()
		{
			var document = AddDocument();
			var sheet = AddSheet(document.Id);
			var entity = AddObject();

			var created = AddLink(document, sheet, entity, 0x542);

			var stored = _repository.CadSymbolLink.GetByBlock(
				document.Id,
				0x542);

			Assert.NotNull(stored);
			Assert.Equal(created.Id, stored!.Id);
			Assert.Equal(document.Id, stored.DocumentId);
			Assert.Equal(sheet.Id, stored.SheetId);
			Assert.Equal(entity.Id, stored.EngineeringObjectId);
			Assert.Equal(0x542L, stored.BlockHandle);
			Assert.NotNull(stored.LoadedVersion);

			Assert.Equal(
				created.Id,
				Assert.Single(
					_repository.CadSymbolLink.GetByDocumentId(document.Id)).Id);

			Assert.Equal(
				created.Id,
				Assert.Single(
					_repository.CadSymbolLink.GetBySheetId(sheet.Id)).Id);

			Assert.Equal(
				created.Id,
				Assert.Single(
					_repository.CadSymbolLink.GetByObjectId(entity.Id)).Id);
		}

		[Fact(DisplayName =	"A CAD symbol link requires a sheet identifier")]
		public void Create_EmptySheetId_IsRejected()
		{
			var document = AddDocument();
			var entity = AddObject();

			var result = _repository.CadSymbolLink.Create(
				document.Id,
				Guid.Empty,
				0x542,
				entity.Id);

			AssertRejected(result, nameof(ICadSymbolLink.SheetId));

			Assert.Null(_repository.CadSymbolLink.GetByBlock(document.Id, 0x542));
		}

		[Fact(DisplayName =
			"A CAD symbol link cannot reference a missing sheet")]
		public void Create_MissingSheet_IsRejected()
		{
			var document = AddDocument();
			var entity = AddObject();

			var result = _repository.CadSymbolLink.Create(
				document.Id,
				Guid.NewGuid(),
				0x542,
				entity.Id);

			AssertRejected(result, nameof(ICadSymbolLink.SheetId));

			Assert.Empty(
				_repository.CadSymbolLink.GetByObjectId(entity.Id));
		}

		[Fact(DisplayName =
			"A CAD symbol link cannot reference a sheet in another document")]
		public void Create_SheetFromAnotherDocument_IsRejected()
		{
			var document = AddDocument();
			var otherDocument = AddDocument();
			var otherSheet = AddSheet(otherDocument.Id);
			var entity = AddObject();

			var result = _repository.CadSymbolLink.Create(
				document.Id,
				otherSheet.Id,
				0x542,
				entity.Id);

			AssertRejected(result, nameof(ICadSymbolLink.SheetId));

			Assert.Empty(
				_repository.CadSymbolLink.GetByDocumentId(document.Id));
		}

		[Fact(DisplayName =
			"A CAD symbol link cannot reference a missing document")]
		public void Create_MissingDocument_IsRejected()
		{
			var document = AddDocument();
			var sheet = AddSheet(document.Id);
			var entity = AddObject();

			var result = _repository.CadSymbolLink.Create(
				Guid.NewGuid(),
				sheet.Id,
				0x542,
				entity.Id);

			AssertRejected(result, nameof(ICadSymbolLink.DocumentId));

			Assert.Empty(
				_repository.CadSymbolLink.GetByObjectId(entity.Id));
		}

		[Fact(DisplayName =
			"A CAD symbol link cannot reference a missing engineering object")]
		public void Create_MissingObject_IsRejected()
		{
			var document = AddDocument();
			var sheet = AddSheet(document.Id);

			var result = _repository.CadSymbolLink.Create(
				document.Id,
				sheet.Id,
				0x542,
				Guid.NewGuid());

			AssertRejected(result, nameof(ICadSymbolLink.EngineeringObjectId));

			Assert.Empty(
				_repository.CadSymbolLink.GetBySheetId(sheet.Id));
		}

		[Fact(DisplayName =
			"A CAD symbol link cannot connect documents and objects from different projects")]
		public void Create_ObjectFromAnotherProject_IsRejected()
		{
			var document = AddDocument(Guid.NewGuid());
			var sheet = AddSheet(document.Id);
			var entity = AddObject();

			var result = _repository.CadSymbolLink.Create(
				document.Id,
				sheet.Id,
				0x542,
				entity.Id);

			Assert.False(result.Validation.IsValid);
			Assert.Null(result.Value);

			Assert.Empty(
				_repository.CadSymbolLink.GetBySheetId(sheet.Id));
		}

		[Fact(DisplayName =
			"The same block handle cannot be registered twice within a document")]
		public void Create_DuplicateHandleAcrossSheets_IsRejected()
		{
			var document = AddDocument();
			var firstSheet = AddSheet(document.Id, "001");
			var secondSheet = AddSheet(document.Id, "002");
			var entity = AddObject();

			var original = AddLink(document, firstSheet, entity, 0x542);

			var result = _repository.CadSymbolLink.Create(
				document.Id,
				secondSheet.Id,
				0x542,
				entity.Id);

			Assert.False(result.Validation.IsValid);
			Assert.Null(result.Value);

			var stored = Assert.Single(
				_repository.CadSymbolLink.GetByDocumentId(document.Id));

			Assert.Equal(original.Id, stored.Id);
			Assert.Equal(firstSheet.Id, stored.SheetId);

			Assert.Empty(
				_repository.CadSymbolLink.GetBySheetId(secondSheet.Id));
		}

		[Fact(DisplayName =
			"The same object can have symbols on multiple sheets and documents")]
		public void Create_MultiplePresentations_PreservesOneObject()
		{
			var firstDocument = AddDocument();
			var secondDocument = AddDocument();

			var firstSheet = AddSheet(firstDocument.Id, "001");
			var secondSheet = AddSheet(firstDocument.Id, "002");
			var thirdSheet = AddSheet(secondDocument.Id, "001");

			var entity = AddObject();

			AddLink(firstDocument, firstSheet, entity, 0x542);
			AddLink(firstDocument, secondSheet, entity, 0x543);

			// Handles are unique within a document, not across documents.
			AddLink(secondDocument, thirdSheet, entity, 0x542);

			Assert.Equal(
				3,
				_repository.CadSymbolLink.GetByObjectId(entity.Id).Count);

			foreach (var sheet in new[] { firstSheet, secondSheet, thirdSheet })
			{
				var stored = Assert.Single(
					_repository.EngineeringObject.GetBySheetId(sheet.Id));

				Assert.Equal(entity.Id, stored.Id);
			}

			Assert.Equal(
				entity.Id,
				Assert.Single(
					_repository.EngineeringObject.GetByProjectId(_projectId)).Id);
		}

		[Fact(DisplayName =
			"Multiple symbols on one sheet return the engineering object only once")]
		public void GetObjectsBySheet_MultipleSymbols_ReturnsDistinctObjects()
		{
			var document = AddDocument();
			var sheet = AddSheet(document.Id);
			var entity = AddObject();

			AddLink(document, sheet, entity, 0x542);
			AddLink(document, sheet, entity, 0x543);

			Assert.Equal(
				2,
				_repository.CadSymbolLink.GetBySheetId(sheet.Id).Count);

			var stored = Assert.Single(
				_repository.EngineeringObject.GetBySheetId(sheet.Id));

			Assert.Equal(entity.Id, stored.Id);
		}

		[Fact(DisplayName =
			"Deleting the last symbol link removes sheet membership but preserves the object")]
		public void Delete_LastLink_PreservesObject()
		{
			var document = AddDocument();
			var sheet = AddSheet(document.Id);
			var entity = AddObject();
			var link = AddLink(document, sheet, entity, 0x542);

			var result = _repository.CadSymbolLink.Delete(link);

			Assert.True(result.IsValid);

			Assert.Empty(
				_repository.CadSymbolLink.GetBySheetId(sheet.Id));

			Assert.Empty(
				_repository.EngineeringObject.GetBySheetId(sheet.Id));

			var stored = _repository.EngineeringObject.GetById(entity.Id);

			Assert.NotNull(stored);
			Assert.False(stored!.IsMarkedForDeletion);
			Assert.Equal(_locationId, stored.LocationId);

			Assert.NotNull(_repository.Sheet.GetById(sheet.Id));
			Assert.NotNull(_repository.Document.GetById(document.Id));
		}

		[Fact(DisplayName =
			"Deleting one symbol preserves other presentations of the same object")]
		public void Delete_OneLink_PreservesOtherPresentations()
		{
			var document = AddDocument();
			var firstSheet = AddSheet(document.Id, "001");
			var secondSheet = AddSheet(document.Id, "002");
			var entity = AddObject();

			var first = AddLink(document, firstSheet, entity, 0x542);
			var second = AddLink(document, firstSheet, entity, 0x543);
			var third = AddLink(document, secondSheet, entity, 0x544);

			Assert.True(
				_repository.CadSymbolLink.Delete(first).IsValid);

			Assert.Equal(
				second.Id,
				Assert.Single(
					_repository.CadSymbolLink.GetBySheetId(firstSheet.Id)).Id);

			Assert.Equal(
				entity.Id,
				Assert.Single(
					_repository.EngineeringObject.GetBySheetId(firstSheet.Id)).Id);

			Assert.True(
				_repository.CadSymbolLink.Delete(second).IsValid);

			Assert.Empty(
				_repository.EngineeringObject.GetBySheetId(firstSheet.Id));

			Assert.Equal(
				third.Id,
				Assert.Single(
					_repository.CadSymbolLink.GetByObjectId(entity.Id)).Id);

			Assert.Equal(
				entity.Id,
				Assert.Single(
					_repository.EngineeringObject.GetBySheetId(secondSheet.Id)).Id);
		}

		[Fact(DisplayName =
			"A sheet with CAD symbol links cannot be deleted")]
		public void DeleteSheet_WithSymbolLink_IsRejected()
		{
			var document = AddDocument();
			var sheet = AddSheet(document.Id);
			var entity = AddObject();
			var link = AddLink(document, sheet, entity, 0x542);

			// Link creation changes the sheet's dependency version.
			var current = _repository.Sheet.GetById(sheet.Id)!;

			var result = _repository.Sheet.Delete(current);

			Assert.False(result.IsValid);
			Assert.NotNull(_repository.Sheet.GetById(sheet.Id));

			Assert.Equal(
				link.Id,
				_repository.CadSymbolLink.GetByBlock(document.Id, 0x542)!.Id);
		}

		[Fact(DisplayName =
			"A sheet can be deleted after its last CAD symbol link is removed")]
		public void DeleteSheet_AfterRemovingLinks_PreservesObject()
		{
			var document = AddDocument();
			var sheet = AddSheet(document.Id);
			var entity = AddObject();
			var link = AddLink(document, sheet, entity, 0x542);

			Assert.True(
				_repository.CadSymbolLink.Delete(link).IsValid);

			var current = _repository.Sheet.GetById(sheet.Id)!;

			Assert.True(_repository.Sheet.Delete(current).IsValid);

			Assert.Null(_repository.Sheet.GetById(sheet.Id));
			Assert.NotNull(_repository.EngineeringObject.GetById(entity.Id));
			Assert.NotNull(_repository.Document.GetById(document.Id));
		}

		[Fact(DisplayName =
			"Marking an object for deletion preserves its CAD symbol and sheet association")]
		public void DeleteObject_PreservesSymbolLink()
		{
			var document = AddDocument();
			var sheet = AddSheet(document.Id);
			var entity = AddObject();
			var link = AddLink(document, sheet, entity, 0x542);

			// Link creation changes the object's dependency version.
			var current = _repository.EngineeringObject.GetById(entity.Id)!;

			Assert.True(
				_repository.EngineeringObject.Delete(current).IsValid);

			var stored = _repository.EngineeringObject.GetById(entity.Id)!;

			Assert.True(stored.IsMarkedForDeletion);

			Assert.Equal(
				link.Id,
				_repository.CadSymbolLink.GetByBlock(document.Id, 0x542)!.Id);

			Assert.Equal(
				entity.Id,
				Assert.Single(
					_repository.EngineeringObject.GetBySheetId(sheet.Id)).Id);

			var currentSheet = _repository.Sheet.GetById(sheet.Id)!;

			Assert.False(
				_repository.Sheet.Delete(currentSheet).IsValid);
		}

		[Fact(DisplayName =
			"Restoring an object preserves its identity and CAD symbol association")]
		public void RestoreObject_PreservesSymbolLink()
		{
			var document = AddDocument();
			var sheet = AddSheet(document.Id);
			var entity = AddObject();
			var link = AddLink(document, sheet, entity, 0x542);

			var current = _repository.EngineeringObject.GetById(entity.Id)!;

			Assert.True(
				_repository.EngineeringObject.Delete(current).IsValid);

			var marked = _repository.EngineeringObject.GetById(entity.Id)!;

			var restored = _repository.EngineeringObject.Restore(marked);

			Assert.True(restored.Validation.IsValid);
			Assert.NotNull(restored.Value);
			Assert.Equal(entity.Id, restored.Value!.Id);

			var stored = _repository.EngineeringObject.GetById(entity.Id)!;

			Assert.False(stored.IsMarkedForDeletion);
			Assert.Null(stored.DeletedAtUtc);
			Assert.Equal(_locationId, stored.LocationId);

			var storedLink = _repository.CadSymbolLink.GetByBlock(
				document.Id,
				0x542);

			Assert.NotNull(storedLink);
			Assert.Equal(link.Id, storedLink!.Id);
			Assert.Equal(sheet.Id, storedLink.SheetId);
			Assert.Equal(entity.Id, storedLink.EngineeringObjectId);
		}

		[Fact(DisplayName =
			"A new CAD symbol link cannot reference an object marked for deletion")]
		public void Create_MarkedObject_IsRejected()
		{
			var document = AddDocument();
			var sheet = AddSheet(document.Id);
			var entity = AddObject();

			Assert.True(
				_repository.EngineeringObject.Delete(entity).IsValid);

			var result = _repository.CadSymbolLink.Create(
				document.Id,
				sheet.Id,
				0x542,
				entity.Id);

			Assert.False(result.Validation.IsValid);
			Assert.Null(result.Value);

			Assert.Empty(
				_repository.CadSymbolLink.GetBySheetId(sheet.Id));

			Assert.True(
				_repository.EngineeringObject
					.GetById(entity.Id)!.IsMarkedForDeletion);
		}

		[Fact(DisplayName =	"Reattaching a disconnected symbol preserves its link and object identities")]
		public void Reattach_DisconnectedLink_PreservesIdentity()
		{
			var document = AddDocument();
			var sheet = AddSheet(document.Id);
			var entity = AddObject();
			var link = AddLink(document, sheet, entity, 0x542);

			Assert.True(
				_repository.CadSymbolLink.Delete(link).IsValid);

			Assert.Empty(
				_repository.EngineeringObject.GetBySheetId(sheet.Id));

			var detached = _repository.CadSymbolLink.GetByBlock(
				document.Id,
				0x542)!;

			Assert.True(detached.IsDetached);

			var result = _repository.CadSymbolLink.Reattach(detached);

			Assert.True(result.IsValid);

			var restored = _repository.CadSymbolLink.GetByBlock(
				document.Id,
				0x542)!;

			Assert.Equal(link.Id, restored.Id);
			Assert.Equal(entity.Id, restored.EngineeringObjectId);
			Assert.Equal(sheet.Id, restored.SheetId);
			Assert.False(restored.IsDetached);
			Assert.Null(restored.DetachedAtUtc);

			Assert.Equal(
				entity.Id,
				Assert.Single(
					_repository.EngineeringObject.GetBySheetId(sheet.Id)).Id);
		}

		[Fact(DisplayName = "A stale symbol link cannot be reattached")]
		public void Reattach_StaleVersion_IsRejected()
		{
			var document = AddDocument();
			var sheet = AddSheet(document.Id);
			var entity = AddObject();
			var link = AddLink(document, sheet, entity, 0x542);

			Assert.True(
				_repository.CadSymbolLink.Delete(link).IsValid);

			// The original snapshot predates the disconnection.
			var result = _repository.CadSymbolLink.Reattach(link);

			Assert.False(result.IsValid);

			var stored = _repository.CadSymbolLink.GetByBlock(
				document.Id,
				0x542)!;

			Assert.True(stored.IsDetached);
		}

		private IDocument AddDocument(Guid? projectId = null)
		{
			var name = $"{Guid.NewGuid():N}.dwg";

			return Require(_repository.Document.Create(
				projectId ?? _projectId,
				_fileFormatId,
				name,
				Path.Combine(_directory, name)));
		}

		private ISheet AddSheet(Guid documentId, string sheetNo = "001")
		{
			return Require(
				_repository.Sheet.Create(documentId, sheetNo));
		}

		private IEngineeringObject AddObject()
		{
			var entity = _repository.EngineeringObject.New(_projectId);
			entity.LocationId = _locationId;

			return Require(
				_repository.EngineeringObject.Create(entity));
		}

		private ICadSymbolLink AddLink(
			IDocument document,
			ISheet sheet,
			IEngineeringObject entity,
			long handle)
		{
			return Require(_repository.CadSymbolLink.Create(
				document.Id,
				sheet.Id,
				handle,
				entity.Id));
		}

		private static T Require<T>(
			(T? Value, ValidationResult Validation) result)
			where T : class
		{
			Assert.True(
				result.Validation.IsValid,
				string.Join(
					Environment.NewLine,
					result.Validation.Errors.Select(error => error.ErrorMessage)));

			Assert.NotNull(result.Value);
			return result.Value!;
		}

		private static void AssertRejected(
			(ICadSymbolLink? Value, ValidationResult Validation) result,
			string propertyName)
		{
			Assert.False(result.Validation.IsValid);
			Assert.Null(result.Value);

			Assert.Contains(
				result.Validation.Errors,
				error => error.PropertyName == propertyName);
		}

		public void Dispose()
		{
			if (Directory.Exists(_directory))
				Directory.Delete(_directory, recursive: true);
		}
	}
}