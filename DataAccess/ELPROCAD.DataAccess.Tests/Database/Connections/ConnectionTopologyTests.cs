using DevExpress.Xpo;
using ELPROCAD.DataAccess.Database;
using ELPROCAD.DataAccess.Models;
using ELPROCAD.DataAccess.Models.MDBX.Connections;
using ELPROCAD.DataAccess.Models.MDBX.Documents;
using ELPROCAD.DataAccess.Models.MDBX.Objects;
using ELPROCAD.DataAccess.Models.MDBX.Terminals;
using ELPROCAD.DataAccess.Repositories.MDBX;
using ELPROCAD.DataAccess.Validation.MDBX.Connections;
using FluentValidation.Results;
using Xunit;

namespace ELPROCAD.DataAccess.Tests.Database.Connections
{
	public sealed partial class ConnectionTopologyTests : IDisposable
	{
		private readonly string _directory = Path.Combine(Path.GetTempPath(), "ELPROCAD.ConnectionTopologyTests", Guid.NewGuid().ToString("N"));
		private readonly string _path;
		private readonly DatabaseAccess _database;
		private readonly IMdbxRepository _mdbx;
		private readonly Guid _projectId = Guid.NewGuid();
		private readonly Guid _sheetId;
		private readonly Guid _symbolId;
		private readonly Guid _terminalId;

		public ConnectionTopologyTests()
		{
			_path = Path.Combine(_directory, "Project.mdbx");
			_mdbx = new DataAccessInitializer().InitializeMdbx(_path);
			_database = new DatabaseAccess(DatabaseConnectionStrings.Create(_path));
			_sheetId = AddSheet(_projectId);
			var entity = new EngineeringObject { ProjectId = _projectId, LocationId = Guid.NewGuid() };
			_database.Execute(uow => { uow.Save(entity); uow.CommitChanges(); });
			var documentId = _mdbx.Sheet.GetById(_sheetId)!.DocumentId;
			var symbol = _mdbx.CadSymbolLink.CreateWithTerminals(documentId, _sheetId, 0x355, entity.Id,
				new[] { ("UTTAG1", "1") });
			Assert.True(symbol.Validation.IsValid);
			_symbolId = symbol.Value!.Id;
			_terminalId = Assert.Single(_mdbx.CadSymbolLink.GetObjectTerminals(entity.Id)).Id;
		}

		[Fact(DisplayName = "Two terminal sides can belong to different local nets without implicit continuity")]
		public void TerminalSides_AreIndependent()
		{
			var first = AddSide("A");
			var second = AddSide("B");
			var a = AddNetwork(_sheetId, 1);
			var b = AddNetwork(_sheetId, 2);
			Save(new ConnectionNetworkPoint { NetworkId = a, PointId = first }, ConnectionTopologyValidation.Validate);
			Save(new ConnectionNetworkPoint { NetworkId = b, PointId = second }, ConnectionTopologyValidation.Validate);
			_database.Execute(uow =>
			{
				Assert.Equal(2, new XPQuery<ConnectionPoint>(uow).Count(x => x.EngineeringObjectTerminalId == _terminalId));
				Assert.Equal(2, new XPQuery<ConnectionNetwork>(uow).Count());
				Assert.Empty(new XPQuery<ConnectionPointLink>(uow).ToList());
			});
		}

		[Fact(DisplayName = "An explicit bridge survives reinitialization without merging local nets")]
		public void ExplicitBridge_RoundTrips()
		{
			var a = AddSide("A");
			var b = AddSide("B");
			Save(new ConnectionNetworkPoint { NetworkId = AddNetwork(_sheetId, 1), PointId = a }, ConnectionTopologyValidation.Validate);
			Save(new ConnectionNetworkPoint { NetworkId = AddNetwork(_sheetId, 2), PointId = b }, ConnectionTopologyValidation.Validate);
			var link = new ConnectionPointLink { Kind = ConnectionPointLinkKind.TerminalBridge };
			link.SetEndpoints(a, b);
			var id = Save(link, ConnectionTopologyValidation.Validate);
			new DataAccessInitializer().InitializeMdbx(_path);
			_database.Execute(uow =>
			{
				var stored = uow.GetObjectByKey<ConnectionPointLink>(id);
				Assert.NotNull(stored);
				Assert.Equal(link.FirstPointId, stored.FirstPointId);
				Assert.Equal(link.SecondPointId, stored.SecondPointId);
				Assert.Equal(ConnectionPointLinkKind.TerminalBridge, stored.Kind);
				Assert.Equal(2, new XPQuery<ConnectionNetwork>(uow).Count());
			});
		}

		[Fact(DisplayName = "An unconnected point is stored without network membership")]
		public void UnconnectedPoint_IsPreserved()
		{
			var id = AddSide("A");
			_database.Execute(uow =>
			{
				Assert.NotNull(uow.GetObjectByKey<ConnectionPoint>(id));
				Assert.Empty(new XPQuery<ConnectionNetworkPoint>(uow).ToList());
			});
		}

		[Fact(DisplayName = "Equal signal names do not establish a connection")]
		public void SignalNames_DoNotAutoConnect()
		{
			AddPoint(_sheetId, ConnectionPointKind.SignalReference, "SIGNAL1");
			AddPoint(AddSheet(_projectId), ConnectionPointKind.SignalReference, "SIGNAL1");
			_database.Execute(uow =>
			{
				Assert.Equal(2, new XPQuery<ConnectionPoint>(uow).Count());
				Assert.Empty(new XPQuery<ConnectionPointLink>(uow).ToList());
			});
		}

		[Fact(DisplayName = "Explicit continuation may cross documents within one project")]
		public void Continuation_CrossDocument_IsAccepted()
		{
			var first = AddPoint(_sheetId, ConnectionPointKind.SignalReference, "S1");
			var second = AddPoint(AddSheet(_projectId), ConnectionPointKind.SignalReference, "S1");
			var link = new ConnectionPointLink { Kind = ConnectionPointLinkKind.SignalContinuation };
			link.SetEndpoints(first, second);
			Save(link, ConnectionTopologyValidation.Validate);
		}

		[Fact(DisplayName = "A continuation cannot connect points from different projects")]
		public void Continuation_CrossProject_IsRejected()
		{
			var first = AddPoint(_sheetId, ConnectionPointKind.SignalReference, "S1");
			var second = AddPoint(AddSheet(Guid.NewGuid()), ConnectionPointKind.SignalReference, "S1");
			var link = new ConnectionPointLink { Kind = ConnectionPointLinkKind.SignalContinuation };
			link.SetEndpoints(first, second);
			_database.Execute(uow => Assert.False(ConnectionTopologyValidation.Validate(uow, link).IsValid));
		}

		[Fact(DisplayName = "Local network membership cannot cross a sheet boundary")]
		public void Membership_CrossSheet_IsRejected()
		{
			var member = new ConnectionNetworkPoint
			{
				NetworkId = AddNetwork(_sheetId, 1),
				PointId = AddPoint(AddSheet(_projectId), ConnectionPointKind.Junction, "")
			};
			_database.Execute(uow => Assert.False(ConnectionTopologyValidation.Validate(uow, member).IsValid));
		}

		[Fact(DisplayName = "A branched net supports more than six connection points")]
		public void BranchedNetwork_HasNoSixPointLimit()
		{
			var network = AddNetwork(_sheetId, 1);
			for (var i = 0; i < 8; i++)
				Save(new ConnectionNetworkPoint
				{
					NetworkId = network,
					PointId = AddPoint(_sheetId, ConnectionPointKind.Junction, i.ToString())
				},
					ConnectionTopologyValidation.Validate);
			_database.Execute(uow => Assert.Equal(8, new XPQuery<ConnectionNetworkPoint>(uow).Count(x => x.NetworkId == network)));
		}

		[Fact(DisplayName = "The database rejects membership of one point in two local nets")]
		public void Membership_IsUniqueInDatabase()
		{
			var point = AddSide("A");
			var first = AddNetwork(_sheetId, 1);
			var second = AddNetwork(_sheetId, 2);
			Save(new ConnectionNetworkPoint { NetworkId = first, PointId = point }, ConnectionTopologyValidation.Validate);
			var error = Assert.ThrowsAny<Exception>(() => Save(
				new ConnectionNetworkPoint { NetworkId = second, PointId = point }, ConnectionTopologyValidation.Validate));
			Assert.True(VistaDbErrors.IsDuplicateKey(error));
		}

		[Fact(DisplayName = "Network numbering is local to a sheet and cannot repeat within that sheet")]
		public void NetworkNumber_IsSheetLocal()
		{
			AddNetwork(_sheetId, 1);
			AddNetwork(AddSheet(_projectId), 1);
			var error = Assert.ThrowsAny<Exception>(() => AddNetwork(_sheetId, 1));
			Assert.True(VistaDbErrors.IsDuplicateKey(error));
		}

		[Fact(DisplayName = "Reversed endpoints do not create a second continuity link")]
		public void LinkPair_IsUndirectedAndUnique()
		{
			var a = AddSide("A");
			var b = AddSide("B");
			var first = new ConnectionPointLink { Kind = ConnectionPointLinkKind.TerminalBridge };
			first.SetEndpoints(a, b);
			Save(first, ConnectionTopologyValidation.Validate);
			var reversed = new ConnectionPointLink { Kind = ConnectionPointLinkKind.TerminalBridge };
			reversed.SetEndpoints(b, a);
			var error = Assert.ThrowsAny<Exception>(() => Save(reversed, ConnectionTopologyValidation.Validate));
			Assert.True(VistaDbErrors.IsDuplicateKey(error));
		}

		[Fact(DisplayName = "Detached CAD symbols cannot supply terminal connection points")]
		public void DetachedSymbol_IsRejected()
		{
			var documentId = _mdbx.Sheet.GetById(_sheetId)!.DocumentId;
			var symbol = _mdbx.CadSymbolLink.GetByBlock(documentId, 0x355)!;
			Assert.True(_mdbx.CadSymbolLink.Delete(symbol).IsValid);
			var point = new ConnectionPoint
			{
				SheetId = _sheetId,
				Kind = ConnectionPointKind.ApparatusTerminal,
				CadSymbolLinkId = _symbolId,
				EngineeringObjectTerminalId = _terminalId
			};
			_database.Execute(uow => Assert.False(ConnectionTopologyValidation.Validate(uow, point).IsValid));
		}

		[Fact(DisplayName = "Missing references, unknown kinds and self-links are rejected")]
		public void InvalidReferences_AreRejected()
		{
			_database.Execute(uow =>
			{
				Assert.False(ConnectionTopologyValidation.Validate(uow, new ConnectionNetwork { SheetId = _sheetId, Number = 0 }).IsValid);
				Assert.False(ConnectionTopologyValidation.Validate(uow, new ConnectionPoint { SheetId = _sheetId, Kind = (ConnectionPointKind)999 }).IsValid);
				Assert.False(ConnectionTopologyValidation.Validate(uow, new ConnectionPoint { SheetId = _sheetId, Kind = ConnectionPointKind.TerminalSide }).IsValid);
				Assert.False(ConnectionTopologyValidation.Validate(uow, new ConnectionNetworkPoint { NetworkId = Guid.NewGuid(), PointId = Guid.NewGuid() }).IsValid);
				var link = new ConnectionPointLink { Kind = ConnectionPointLinkKind.TerminalBridge };
				var id = Guid.NewGuid();
				link.SetEndpoints(id, id);
				Assert.False(ConnectionTopologyValidation.Validate(uow, link).IsValid);
			});
		}

		[Fact(DisplayName = "PE supports multiple connection points without the apparatus terminal conductor limit")]
		public void Potential_HasNoTerminalLimit()
		{
			var potentialId = Save(new ElectricalPotential { ProjectId = _projectId, Name = "PE" }, ConnectionTopologyValidation.Validate);
			var network = AddNetwork(_sheetId, 1);
			for (var i = 0; i < 12; i++)
			{
				var point = Save(new ConnectionPoint
				{
					SheetId = _sheetId,
					Kind = ConnectionPointKind.Potential,
					ElectricalPotentialId = potentialId
				},
					ConnectionTopologyValidation.Validate);
				Save(new ConnectionNetworkPoint { NetworkId = network, PointId = point }, ConnectionTopologyValidation.Validate);
			}
			_database.Execute(uow =>
			{
				Assert.Equal(12, new XPQuery<ConnectionPoint>(uow).Count(x => x.ElectricalPotentialId == potentialId));
				Assert.Equal(12, new XPQuery<ConnectionNetworkPoint>(uow).Count(x => x.NetworkId == network));
			});
		}

		[Fact(DisplayName = "Terminal conductor limits default to two and retain a configured override")]
		public void TerminalLimit_DefaultOverrideAndReset()
		{
			_database.Execute(uow =>
			{
				var terminal = uow.GetObjectByKey<EngineeringObjectTerminal>(_terminalId);
				Assert.Null(terminal.ConfiguredMaxWireConnections);
				Assert.Equal(2, terminal.MaxWireConnections);
				Assert.Throws<ArgumentOutOfRangeException>(() => terminal.ConfiguredMaxWireConnections = 0);
				terminal.ConfiguredMaxWireConnections = 4;
				uow.Save(terminal);
				uow.CommitChanges();
			});
			new DataAccessInitializer().InitializeMdbx(_path);
			_database.Execute(uow =>
			{
				var terminal = uow.GetObjectByKey<EngineeringObjectTerminal>(_terminalId);
				Assert.Equal(4, terminal.MaxWireConnections);
				terminal.ConfiguredMaxWireConnections = null;
				uow.Save(terminal);
				uow.CommitChanges();
			});
			_database.Execute(uow => Assert.Equal(2, uow.GetObjectByKey<EngineeringObjectTerminal>(_terminalId).MaxWireConnections));
		}

		[Fact(DisplayName = "Potential points cannot reference another project's potential or mix potential and terminal identity")]
		public void PotentialReferences_AreValidated()
		{
			var potential = Save(new ElectricalPotential { ProjectId = Guid.NewGuid(), Name = "PE" }, ConnectionTopologyValidation.Validate);
			_database.Execute(uow =>
			{
				Assert.False(ConnectionTopologyValidation.Validate(uow, new ConnectionPoint
				{
					SheetId = _sheetId,
					Kind = ConnectionPointKind.Potential,
					ElectricalPotentialId = potential
				}).IsValid);
				Assert.False(ConnectionTopologyValidation.Validate(uow, new ConnectionPoint
				{
					SheetId = _sheetId,
					Kind = ConnectionPointKind.Potential
				}).IsValid);
				Assert.False(ConnectionTopologyValidation.Validate(uow, new ConnectionPoint
				{
					SheetId = _sheetId,
					Kind = ConnectionPointKind.TerminalSide,
					CadSymbolLinkId = _symbolId,
					EngineeringObjectTerminalId = _terminalId,
					ElectricalPotentialId = potential
				}).IsValid);
			});
		}

		[Fact(DisplayName = "A contact picture must not be mistaken for an electrical signal reference")]
		public void ContactPicture_IsNotElectricalReference()
		{
			var sheet = _mdbx.Sheet.GetById(_sheetId)!;
			var source = _mdbx.CadSymbolLink.GetByBlock(sheet.DocumentId, 0x355)!;
			var picture = _mdbx.CadSymbolLink.CreateReferenceContact(sheet.DocumentId, _sheetId, 0x356,
				source.EngineeringObjectId, "EPCBI05C", "Q03", "", "", new[] { ("UTTAG1", "1") });
			Assert.True(picture.Validation.IsValid);
			_database.Execute(uow => Assert.False(ConnectionTopologyValidation.Validate(uow, new ConnectionPoint
			{
				SheetId = _sheetId,
				Kind = ConnectionPointKind.SignalReference,
				CadSymbolLinkId = picture.Value!.Id
			}).IsValid));
		}

		[Fact(DisplayName = "Cable ends remain distinct points until an explicit cable-core link is stored")]
		public void CableEnds_RequireExplicitLink()
		{
			var a = AddPoint(_sheetId, ConnectionPointKind.CableEnd, "1");
			var b = AddPoint(AddSheet(_projectId), ConnectionPointKind.CableEnd, "1");
			_database.Execute(uow => Assert.Empty(new XPQuery<ConnectionPointLink>(uow).ToList()));
			var link = new ConnectionPointLink { Kind = ConnectionPointLinkKind.CableCore };
			link.SetEndpoints(a, b);
			var id = Save(link, ConnectionTopologyValidation.Validate);
			_database.Execute(uow => Assert.Equal(ConnectionPointLinkKind.CableCore,
				uow.GetObjectByKey<ConnectionPointLink>(id).Kind));
		}

		private Guid AddSheet(Guid projectId)
		{
			var document = new Document { ProjectId = projectId, Name = Guid.NewGuid() + ".dwg", Path = "Test.dwg" };
			var sheet = new Sheet { DocumentId = document.Id, SheetNo = "001" };
			_database.Execute(uow => { uow.Save(document); uow.Save(sheet); uow.CommitChanges(); });
			return sheet.Id;
		}

		private Guid AddNetwork(Guid sheetId, int number) => Save(
			new ConnectionNetwork { SheetId = sheetId, Number = number }, ConnectionTopologyValidation.Validate);

		private Guid AddSide(string name) => Save(new ConnectionPoint
		{
			SheetId = _sheetId,
			Kind = ConnectionPointKind.TerminalSide,
			Name = name,
			CadSymbolLinkId = _symbolId,
			EngineeringObjectTerminalId = _terminalId
		}, ConnectionTopologyValidation.Validate);

		private Guid AddPoint(Guid sheetId, ConnectionPointKind kind, string name) => Save(
			new ConnectionPoint { SheetId = sheetId, Kind = kind, Name = name }, ConnectionTopologyValidation.Validate);

		private Guid Save<T>(T value, Func<UnitOfWork, T, ValidationResult> validate) where T : VersionedEntity
		{
			return _database.Execute(uow =>
			{
				var result = validate(uow, value);
				Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(x => x.ErrorMessage)));
				uow.Save(value);
				uow.CommitChanges();
				return value.Id;
			});
		}

		public void Dispose()
		{
			if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
		}
	}
}
