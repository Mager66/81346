using DevExpress.Xpo;
using ELPROCAD.DataAccess.Models.MDBX.Connections;
using ELPROCAD.DataAccess.Models.MDBX.Objects;
using ELPROCAD.DataAccess.Repositories.MDBX.Connections;
using FluentValidation.Results;
using Xunit;

namespace ELPROCAD.DataAccess.Tests.Database.Connections
{
	public sealed partial class ConnectionTopologyTests
	{
		private ValidationResult ReplaceAnalysis(int? number = 1, Action? afterFlush = null)
		{
			var sheet = _mdbx.Sheet.GetById(_sheetId)!;
			var symbol = _mdbx.CadSymbolLink.GetByBlock(sheet.DocumentId, 0x355)!;
			return new ConnectionAnalysisRepository(_database, afterFlush).Replace(sheet,
				number.HasValue ? new[] { number.Value } : Array.Empty<int>(),
				new[] { (_symbolId, symbol.LoadedVersion!.Value, "UTTAG1", _terminalId, number) });
		}

		[Fact(DisplayName = "A repeated analysis retains point identity and explicit links")]
		public void Analysis_Repeated_PreservesIdentity()
		{
			Assert.True(ReplaceAnalysis().IsValid);
			var pointId = _database.Execute(u => new XPQuery<ConnectionPoint>(u).Single().Id);
			var other = AddPoint(_sheetId, ConnectionPointKind.Junction, "Manual point");
			_database.Execute(u => {
				var link = new ConnectionPointLink { Kind = ConnectionPointLinkKind.TerminalBridge };
				link.SetEndpoints(pointId, other); u.Save(link); u.CommitChanges();
			});
			Assert.True(ReplaceAnalysis(2).IsValid);
			_database.Execute(u => {
				Assert.Equal(pointId, new XPQuery<ConnectionPoint>(u).Single(x => x.AnalysisTag == "UTTAG1").Id);
				Assert.Single(new XPQuery<ConnectionPointLink>(u).ToList());
				Assert.Equal(2, new XPQuery<ConnectionNetwork>(u).Single().Number);
			});
		}

		[Fact(DisplayName = "Unconnected terminals remain stored without membership")]
		public void Analysis_Unconnected_PreservesPoint()
		{
			Assert.True(ReplaceAnalysis().IsValid);
			Assert.True(ReplaceAnalysis(null).IsValid);
			_database.Execute(u => {
				Assert.Single(new XPQuery<ConnectionPoint>(u).ToList());
				Assert.Empty(new XPQuery<ConnectionNetworkPoint>(u).ToList());
				Assert.Empty(new XPQuery<ConnectionNetwork>(u).ToList());
			});
		}

		[Fact(DisplayName = "A failure after flushing deletions rolls the entire connection transaction back")]
		public void Analysis_FailureAfterFlush_RollsBack()
		{
			Assert.True(ReplaceAnalysis().IsValid);
			Assert.Throws<InvalidOperationException>(() => ReplaceAnalysis(2,
				() => throw new InvalidOperationException("Injected failure.")));
			_database.Execute(u => {
				Assert.Equal(1, new XPQuery<ConnectionNetwork>(u).Single().Number);
				Assert.Single(new XPQuery<ConnectionNetworkPoint>(u).ToList());
				Assert.Single(new XPQuery<ConnectionPoint>(u).ToList());
			});
		}

		[Fact(DisplayName = "Stale sheet versions cannot overwrite a newer analysis")]
		public void Analysis_StaleSheet_IsRejected()
		{
			var sheet = _mdbx.Sheet.GetById(_sheetId)!;
			Assert.True(ReplaceAnalysis().IsValid);
			var symbol = _mdbx.CadSymbolLink.GetByBlock(sheet.DocumentId, 0x355)!;
			var result = _mdbx.ConnectionAnalysis.Replace(sheet, new[] { 2 },
				new[] { (_symbolId, symbol.LoadedVersion!.Value, "UTTAG1", _terminalId, (int?)2) });
			Assert.False(result.IsValid);
			_database.Execute(u => Assert.Equal(1, new XPQuery<ConnectionNetwork>(u).Single().Number));
		}

		[Fact(DisplayName = "Missing terminal mappings cannot erase the previous analysis")]
		public void Analysis_IncompleteSnapshot_IsRejected()
		{
			Assert.True(ReplaceAnalysis().IsValid);
			var result = _mdbx.ConnectionAnalysis.Replace(_mdbx.Sheet.GetById(_sheetId)!, Array.Empty<int>(),
				Array.Empty<(Guid, int, string, Guid, int?)>());
			Assert.False(result.IsValid);
			_database.Execute(u => Assert.Single(new XPQuery<ConnectionNetworkPoint>(u).ToList()));
		}

		[Fact(DisplayName = "A collision with a manual network rolls back deleted memberships")]
		public void Analysis_ManualNetworkConflict_RollsBack()
		{
			Assert.True(ReplaceAnalysis().IsValid);
			AddNetwork(_sheetId, 2);
			Assert.False(ReplaceAnalysis(2).IsValid);
			_database.Execute(u => {
				Assert.Equal(2, new XPQuery<ConnectionNetwork>(u).Count());
				Assert.Single(new XPQuery<ConnectionNetworkPoint>(u).ToList());
			});
		}

		[Fact(DisplayName = "Empty local nets are persisted and block sheet deletion")]
		public void Analysis_EmptyNetwork_IsStored()
		{
			var sheetId = AddSheet(_projectId);
			var result = _mdbx.ConnectionAnalysis.Replace(_mdbx.Sheet.GetById(sheetId)!, new[] { 1, 2 },
				Array.Empty<(Guid, int, string, Guid, int?)>());
			Assert.True(result.IsValid);
			Assert.False(_mdbx.Sheet.Delete(_mdbx.Sheet.GetById(sheetId)!).IsValid);
			_database.Execute(u => Assert.Equal(2, new XPQuery<ConnectionNetwork>(u).Count(x => x.SheetId == sheetId)));
		}

		[Fact(DisplayName = "Stale symbol versions cannot replace connection data")]
		public void Analysis_StaleSymbol_IsRejected()
		{
			var sheet = _mdbx.Sheet.GetById(_sheetId)!;
			var symbol = _mdbx.CadSymbolLink.GetByBlock(sheet.DocumentId, 0x355)!;
			_database.Execute(u => {
				var stored = u.GetObjectByKey<CadSymbolLink>(_symbolId);
				stored.DependencyRevision++; u.Save(stored); u.CommitChanges();
			});
			Assert.False(_mdbx.ConnectionAnalysis.Replace(sheet, new[] { 1 },
				new[] { (_symbolId, symbol.LoadedVersion!.Value, "UTTAG1", _terminalId, (int?)1) }).IsValid);
			_database.Execute(u => Assert.Empty(new XPQuery<ConnectionNetwork>(u).ToList()));
		}

		[Fact(DisplayName = "Explicit links prevent removal of obsolete analysis points")]
		public void Analysis_ObsoleteLinkedPoint_IsRejected()
		{
			Assert.True(ReplaceAnalysis().IsValid);
			_database.Execute(u => {
				var point = new XPQuery<ConnectionPoint>(u).Single();
				var other = new ConnectionPoint { SheetId = _sheetId, Kind = ConnectionPointKind.Junction };
				var link = new ConnectionPointLink { Kind = ConnectionPointLinkKind.TerminalBridge };
				link.SetEndpoints(point.Id, other.Id);
				var symbol = u.GetObjectByKey<CadSymbolLink>(_symbolId);
				symbol.DetachedAtUtc = DateTime.UtcNow;
				u.Save(symbol); u.Save(other); u.Save(link); u.CommitChanges();
			});
			Assert.False(_mdbx.ConnectionAnalysis.Replace(_mdbx.Sheet.GetById(_sheetId)!, Array.Empty<int>(),
				Array.Empty<(Guid, int, string, Guid, int?)>()).IsValid);
			_database.Execute(u => {
				Assert.Single(new XPQuery<ConnectionPointLink>(u).ToList());
				Assert.Single(new XPQuery<ConnectionNetworkPoint>(u).ToList());
			});
		}

		[Fact(DisplayName = "A terminal cannot be assigned to two nets in one snapshot")]
		public void Analysis_DuplicateSlot_IsRejected()
		{
			var sheet = _mdbx.Sheet.GetById(_sheetId)!;
			var symbol = _mdbx.CadSymbolLink.GetByBlock(sheet.DocumentId, 0x355)!;
			Assert.False(_mdbx.ConnectionAnalysis.Replace(sheet, new[] { 1, 2 }, new[] {
				(_symbolId, symbol.LoadedVersion!.Value, "UTTAG1", _terminalId, (int?)1),
				(_symbolId, symbol.LoadedVersion!.Value, "uttag1", _terminalId, (int?)2) }).IsValid);
			_database.Execute(u => Assert.Empty(new XPQuery<ConnectionNetwork>(u).ToList()));
		}

		[Fact(DisplayName = "Replacing one sheet does not change another sheet's local networks")]
		public void Analysis_IsSheetScoped()
		{
			var other = AddSheet(_projectId);
			AddNetwork(other, 1);
			Assert.True(ReplaceAnalysis().IsValid);
			Assert.True(ReplaceAnalysis(2).IsValid);
			_database.Execute(u => Assert.Equal(1,
				new XPQuery<ConnectionNetwork>(u).Single(x => x.SheetId == other).Number));
		}
	}
}
