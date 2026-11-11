using System;
using System.IO;
using System.Linq;
using DevExpress.Xpo;
using ELPROCAD.DataAccess.Database;
using ELPROCAD.DataAccess.Models.MDBX.Objects;
using Xunit;

namespace ELPROCAD.DataAccess.Tests.Database
{
	public sealed class CadSymbolLinkStorageTests : IDisposable
	{
		private readonly string _directory = Path.Combine(
			Path.GetTempPath(),
			"ELPROCAD.DataAccess.Tests",
			Guid.NewGuid().ToString("N"));

		private readonly string _databasePath;
		private readonly DatabaseAccess _database;

		public CadSymbolLinkStorageTests()
		{
			_databasePath = Path.Combine(_directory, "Project.mdbx");

			var initializer = new DataAccessInitializer();
			initializer.InitializeMdbx(_databasePath);

			_database = new DatabaseAccess(
				DatabaseConnectionStrings.Create(_databasePath));
		}

		private Guid SaveLink(
			Guid documentId,
			long blockHandle,
			Guid engineeringObjectId)
		{
			return _database.Execute(uow =>
			{
				var link = new CadSymbolLink
				{
					DocumentId = documentId,
					BlockHandle = blockHandle,
					EngineeringObjectId = engineeringObjectId
				};

				uow.Save(link);
				uow.CommitChanges();

				return link.Id;
			});
		}

		[Fact(DisplayName =
			"A CAD symbol link preserves its identifiers after reopening")]
		public void Save_Reopen_PreservesIdentifiers()
		{
			var documentId = Guid.NewGuid();
			var objectId = Guid.NewGuid();
			const long handle = 0x63F;

			var linkId = SaveLink(documentId, handle, objectId);

			_database.Execute(uow =>
			{
				var stored = uow.GetObjectByKey<CadSymbolLink>(linkId);

				Assert.NotNull(stored);
				Assert.Equal(documentId, stored!.DocumentId);
				Assert.Equal(handle, stored.BlockHandle);
				Assert.Equal(objectId, stored.EngineeringObjectId);
			});
		}

		[Fact(DisplayName =
			"A block handle can only have one link within the same document")]
		public void DuplicateDocumentAndHandle_IsRejected()
		{
			var documentId = Guid.NewGuid();
			var originalObjectId = Guid.NewGuid();

			var originalId = SaveLink(
				documentId,
				0x63F,
				originalObjectId);

			var exception = Record.Exception(() =>
			{
				SaveLink(documentId, 0x63F, Guid.NewGuid());
			});

			Assert.NotNull(exception);
			Assert.True(VistaDbErrors.IsDuplicateKey(exception));

			_database.Execute(uow =>
			{
				var links = new XPQuery<CadSymbolLink>(uow).ToList();

				var stored = Assert.Single(links);

				Assert.Equal(originalId, stored.Id);
				Assert.Equal(originalObjectId, stored.EngineeringObjectId);
			});
		}

		[Fact(DisplayName =
			"The same block handle is allowed in different documents")]
		public void SameHandle_DifferentDocuments_IsAllowed()
		{
			var firstDocumentId = Guid.NewGuid();
			var secondDocumentId = Guid.NewGuid();

			SaveLink(firstDocumentId, 0x63F, Guid.NewGuid());
			SaveLink(secondDocumentId, 0x63F, Guid.NewGuid());

			_database.Execute(uow =>
			{
				var links = new XPQuery<CadSymbolLink>(uow).ToList();

				Assert.Equal(2, links.Count);
				Assert.Contains(
					links,
					link => link.DocumentId == firstDocumentId);
				Assert.Contains(
					links,
					link => link.DocumentId == secondDocumentId);
			});
		}

		[Fact(DisplayName =
			"Multiple CAD symbols can reference the same engineering object")]
		public void DifferentHandles_SameObject_IsAllowed()
		{
			var documentId = Guid.NewGuid();
			var objectId = Guid.NewGuid();

			SaveLink(documentId, 0x63F, objectId);
			SaveLink(documentId, 0x640, objectId);

			_database.Execute(uow =>
			{
				var links = new XPQuery<CadSymbolLink>(uow).ToList();

				Assert.Equal(2, links.Count);
				Assert.All(
					links,
					link => Assert.Equal(
						objectId,
						link.EngineeringObjectId));
			});
		}

		[Fact(DisplayName =
			"MDBX reinitialization preserves CAD symbol links")]
		public void Reinitialize_PreservesLinks()
		{
			var linkId = SaveLink(
				Guid.NewGuid(),
				0x63F,
				Guid.NewGuid());

			var initializer = new DataAccessInitializer();
			initializer.InitializeMdbx(_databasePath);

			_database.Execute(uow =>
			{
				Assert.NotNull(
					uow.GetObjectByKey<CadSymbolLink>(linkId));
			});
		}

		public void Dispose()
		{
			if (Directory.Exists(_directory))
				Directory.Delete(_directory, recursive: true);
		}
	}
}