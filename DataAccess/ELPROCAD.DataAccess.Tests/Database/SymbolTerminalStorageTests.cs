using DevExpress.Xpo;
using DevExpress.Xpo.DB;
using ELPROCAD.DataAccess.Contracts.MDBX.Objects;
using ELPROCAD.DataAccess.CRUD;
using ELPROCAD.DataAccess.Database;
using ELPROCAD.DataAccess.Models.MDBX.Documents;
using ELPROCAD.DataAccess.Models.MDBX.Objects;
using ELPROCAD.DataAccess.Models.MDBX.References;
using ELPROCAD.DataAccess.Models.MDBX.Terminals;
using ELPROCAD.DataAccess.Repositories.MDBX.Objects;
using Xunit;

namespace ELPROCAD.DataAccess.Tests.Database
{
	public sealed class SymbolTerminalStorageTests : IDisposable
	{
		private readonly string _directory = Path.Combine(Path.GetTempPath(),
			"ELPROCAD.TerminalStorageTests", Guid.NewGuid().ToString("N"));
		private readonly DatabaseAccess _database;
		private readonly CadSymbolLinkRepository _repository;
		private readonly Guid _documentId;
		private readonly Guid _sheetId;
		private readonly Guid _objectId;

		public SymbolTerminalStorageTests()
		{
			Directory.CreateDirectory(_directory);
			var connection = DatabaseConnectionStrings.Create(Path.Combine(_directory, "Test.mdbx"));
			using (var layer = XpoDefault.GetDataLayer(connection, AutoCreateOption.DatabaseAndSchema))
			using (var uow = new UnitOfWork(layer))
			{
				uow.UpdateSchema(
					typeof(Document),
					typeof(Sheet),
					typeof(EngineeringObject),
					typeof(CadSymbolLink),
					typeof(EngineeringObjectTerminal),
					typeof(CadSymbolTerminal),
					typeof(CadReferenceContact),
					typeof(CadReferenceContactTerminal));


				var projectId = Guid.NewGuid();
				var document = new Document { ProjectId = projectId, Name = "Test", Path = "Test.dwg" };
				var sheet = new Sheet { DocumentId = document.Id, SheetNo = "001" };
				var owner = new EngineeringObject { ProjectId = projectId, LocationId = Guid.NewGuid() };
				uow.Save(document); uow.Save(sheet); uow.Save(owner); uow.CommitChanges();
				_documentId = document.Id; _sheetId = sheet.Id; _objectId = owner.Id;
			}
			_database = new DatabaseAccess(connection);
			_repository = new CadSymbolLinkRepository(new Read(_database), new Create(_database), new Update(_database));
		}

		[Fact(DisplayName = "Creating a symbol persists its unconnected terminals in the same operation")]
		public void CreateWithTerminals_PersistsAll()
		{
			var result = _repository.CreateWithTerminals(_documentId, _sheetId, 99, _objectId,
				new[] { ("UTTAG1", "A1"), ("UTTAG2", "A2") });
			Assert.True(result.Validation.IsValid, string.Join("; ", result.Validation.Errors));
			Assert.NotNull(result.Value);
			Assert.Equal(2, _repository.GetTerminals(result.Value!.Id).Count);
			Assert.Equal(2, _repository.GetObjectTerminals(_objectId).Count);
		}

		[Fact(DisplayName = "A terminal conflict during symbol creation leaves no link or partial catalog entry")]
		public void CreateWithTerminals_ConflictDoesNotCreateLink()
		{
			Save(Add(1), ("UTTAG1", "A1"));
			var result = _repository.CreateWithTerminals(_documentId, _sheetId, 99, _objectId,
				new[] { ("UTTAG1", "NEW"), ("UTTAG2", "A1") });
			Assert.False(result.Validation.IsValid);
			Assert.Null(result.Value);
			Assert.Null(_repository.GetByBlock(_documentId, 99));
			Assert.Equal("A1", Assert.Single(_repository.GetObjectTerminals(_objectId)).Designation);
		}

		[Fact(DisplayName = "Different terminal values allow two presentations of one engineering object")]
		public void CreateWithTerminals_DistinctValuesShareObject()
		{
			Save(Add(1), ("UTTAG1", "A1"), ("UTTAG2", "A2"));
			var result = _repository.CreateWithTerminals(_documentId, _sheetId, 99, _objectId,
				new[] { ("UTTAG1", "13"), ("UTTAG2", "14") });
			Assert.True(result.Validation.IsValid, string.Join("; ", result.Validation.Errors));
			Assert.Equal(_objectId, result.Value!.EngineeringObjectId);
			Assert.Equal(4, _repository.GetObjectTerminals(_objectId).Count);
		}

		private ICadSymbolLink Add(long handle)
		{
			var result = _repository.Create(_documentId, _sheetId, handle, _objectId);
			Assert.True(result.Validation.IsValid, string.Join("; ", result.Validation.Errors));
			return result.Value!;
		}
		private ICadSymbolLink Reload(ICadSymbolLink link) => _repository.GetByBlock(link.DocumentId, link.BlockHandle)!;
		private ICadSymbolLink Save(ICadSymbolLink link, params (string Tag, string Designation)[] values)
		{
			var result = _repository.ReplaceTerminals(link, values);
			Assert.True(result.Validation.IsValid, string.Join("; ", result.Validation.Errors));
			return result.Value!;
		}

		[Fact(DisplayName = "Unconnected terminals persist and survive replacement without a six-slot limit")]
		public void Replace_PersistsTwelveSlots()
		{
			var symbol = Add(1);
			var values = Enumerable.Range(1, 12).Select(n => ("PIN" + n, "T" + n)).ToArray();
			symbol = Save(symbol, values);
			Assert.Equal(12, _repository.GetTerminals(symbol.Id).Count);
			Assert.Equal(12, _repository.GetObjectTerminals(_objectId).Count);
			Assert.NotNull(symbol.LoadedVersion);
		}

		[Fact(DisplayName = "Reanalysis retains slot identities and supports swapping terminal values")]
		public void Replace_SwapsValues()
		{
			var symbol = Save(Add(1), ("UTTAG1", "A1"), ("UTTAG2", "A2"));
			var original = _repository.GetTerminals(symbol.Id).ToDictionary(x => x.Tag);
			symbol = Save(symbol, ("UTTAG1", "A2"), ("UTTAG2", "A1"));
			var changed = _repository.GetTerminals(symbol.Id).ToDictionary(x => x.Tag);
			Assert.Equal(original["UTTAG1"].Id, changed["UTTAG1"].Id);
			Assert.Equal(original["UTTAG2"].EngineeringObjectTerminalId, changed["UTTAG1"].EngineeringObjectTerminalId);
		}

		[Fact(DisplayName = "An occupancy conflict leaves old mappings, catalog and versions unchanged")]
		public void Replace_ConflictRollsBack()
		{
			var first = Save(Add(1), ("UTTAG1", "A1"));
			var second = Save(Add(2), ("UTTAG1", "13"));
			var original = Assert.Single(_repository.GetTerminals(second.Id));
			var version = Reload(second).LoadedVersion;
			var result = _repository.ReplaceTerminals(second,
				new[] { ("UTTAG1", "NEW"), ("UTTAG2", "A1") });
			Assert.False(result.Validation.IsValid);
			Assert.Equal(original.Id, Assert.Single(_repository.GetTerminals(second.Id)).Id);
			Assert.DoesNotContain(_repository.GetObjectTerminals(_objectId), x => x.Designation == "NEW");
			Assert.Equal(version, Reload(second).LoadedVersion);
			Assert.Single(_repository.GetTerminals(first.Id));
		}

		[Fact(DisplayName = "Replacing a slot removes its old mapping but preserves the old object terminal")]
		public void Replace_RemovesObsoleteSlot()
		{
			var symbol = Save(Add(1), ("UTTAG1", "A1"), ("UTTAG2", "A2"));
			symbol = Save(symbol, ("PIN9", "13"));
			var mapping = Assert.Single(_repository.GetTerminals(symbol.Id));
			Assert.Equal("PIN9", mapping.Tag);
			Assert.Equal(3, _repository.GetObjectTerminals(_objectId).Count);
			Save(Add(2), ("UTTAG1", "A1"), ("UTTAG2", "A2"));
		}

		[Fact(DisplayName = "Whitespace or empty terminal values do not create catalog entries")]
		public void Replace_EmptyValueRejected()
		{
			var symbol = Add(1);
			Assert.False(_repository.ReplaceTerminals(symbol, new[] { ("UTTAG1", " ") }).Validation.IsValid);
			Assert.Empty(_repository.GetObjectTerminals(_objectId));
		}

		[Fact(DisplayName = "A detached symbol cannot replace its terminal mappings")]
		public void Replace_DetachedRejected()
		{
			var symbol = Save(Add(1), ("UTTAG1", "A1"));
			Assert.True(_repository.Delete(symbol).IsValid);
			Assert.False(_repository.ReplaceTerminals(Reload(symbol), new[] { ("UTTAG1", "A2") }).Validation.IsValid);
			Assert.Equal("A1", Assert.Single(_repository.GetObjectTerminals(_objectId)).Designation);
		}

		[Fact(DisplayName = "Duplicate raw tags are rejected before persistence")]
		public void Replace_DuplicateTag()
		{
			var symbol = Add(1);
			Assert.False(_repository.ReplaceTerminals(symbol,
				new[] { ("UTTAG1", "A1"), ("UTTAG1", "A2") }).Validation.IsValid);
			Assert.Empty(_repository.GetTerminals(symbol.Id));
			Assert.Empty(_repository.GetObjectTerminals(_objectId));
		}

		[Fact(DisplayName = "An empty replacement releases slots while retaining the object's terminal catalog")]
		public void Replace_EmptyReleases()
		{
			var symbol = Save(Add(1), ("UTTAG1", "A1"));
			symbol = Save(symbol);
			Assert.Empty(_repository.GetTerminals(symbol.Id));
			Assert.Single(_repository.GetObjectTerminals(_objectId));
			Save(Add(2), ("UTTAG1", "A1"));
		}

		[Fact(DisplayName = "A stale symbol cannot replace a more recent terminal list")]
		public void Replace_StaleVersion()
		{
			var original = Add(1);
			Save(original, ("UTTAG1", "A1"));
			Assert.False(_repository.ReplaceTerminals(original, new[] { ("UTTAG1", "A2") }).Validation.IsValid);
			Assert.Equal("A1", Assert.Single(_repository.GetObjectTerminals(_objectId)).Designation);
		}

		[Fact(DisplayName = "Reattachment rejects a terminal claimed while the original presentation was detached")]
		public void Reattach_Conflicts()
		{
			var first = Save(Add(1), ("UTTAG1", "A1"));
			Assert.True(_repository.Delete(first).IsValid);
			var second = Save(Add(2), ("UTTAG1", "A1"));
			var detached = Reload(first);
			Assert.False(_repository.Reattach(detached).IsValid);
			Assert.True(Reload(first).IsDetached);
			Assert.True(_repository.Delete(second).IsValid);
			Assert.True(_repository.Reattach(Reload(first)).IsValid);
		}

		[Fact(DisplayName = "Concurrent claims validated before either commit cannot both succeed")]
		public void ConcurrentClaims_OnlyOneCommits()
		{
			var first = Add(1); var second = Add(2);
			// Pre-create the terminal so this tests occupancy locking, not unique-key insertion.
			_database.Execute(uow => {
				uow.Save(new EngineeringObjectTerminal
				{
					EngineeringObjectId = _objectId,
					Designation = "A1"
				}); uow.CommitChanges();
			});
			var delayedDatabase = new InterleavingDatabase(_database);
			var otherRepository = new CadSymbolLinkRepository(new Read(delayedDatabase),
				new Create(delayedDatabase), new Update(delayedDatabase));
			delayedDatabase.AfterStaging = () => Save(second, ("UTTAG1", "A1"));
			var result = otherRepository.ReplaceTerminals(first, new[] { ("UTTAG1", "A1") });
			Assert.False(result.Validation.IsValid);
			Assert.Empty(_repository.GetTerminals(first.Id));
			Assert.Single(_repository.GetTerminals(second.Id));
		}

		// Interleave immediately before Update.Save commits, without changing production code.
		private sealed class InterleavingDatabase : IDatabaseAccess
		{
			private readonly IDatabaseAccess _inner;
			internal Action? AfterStaging;
			internal InterleavingDatabase(IDatabaseAccess inner) => _inner = inner;
			public T Execute<T>(Func<UnitOfWork, T> operation) => _inner.Execute(uow =>
			{
				SessionManipulationEventHandler handler = (_, __) => {
					var callback = AfterStaging; AfterStaging = null; callback?.Invoke();
				};
				uow.BeforeCommitTransaction += handler;
				try { return operation(uow); }
				finally { uow.BeforeCommitTransaction -= handler; }
			});
			public void Execute(Action<UnitOfWork> operation) => Execute<object?>(uow => { operation(uow); return null; });
			public T ExecuteExplicit<T>(Func<ExplicitUnitOfWork, T> operation) => _inner.ExecuteExplicit(operation);
		}

		public void Dispose()
		{
			// Only the unique directory created by this fixture is removed.
			if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
		}
	}
}



