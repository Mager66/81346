using System;
using System.IO;
using System.Linq;
using DevExpress.Xpo;
using ELPROCAD.DataAccess.Contracts.MDBX.Objects;
using ELPROCAD.DataAccess.CRUD;
using ELPROCAD.DataAccess.Database;
using ELPROCAD.DataAccess.Models.MDBX.Documents;
using ELPROCAD.DataAccess.Models.MDBX.Objects;
using ELPROCAD.DataAccess.Models.MDBX.References;
using ELPROCAD.DataAccess.Repositories.MDBX;
using Xunit;

namespace ELPROCAD.DataAccess.Tests.Database
{
	public sealed class CadReferenceContactStorageTests : IDisposable
	{
		private readonly string _directory = Path.Combine(
			Path.GetTempPath(),
			"ELPROCAD.ReferenceContactTests",
			Guid.NewGuid().ToString("N"));

		private readonly string _path;
		private readonly DatabaseAccess _database;
		private readonly Read _read;
		private readonly IMdbxRepository _mdbx;
		private readonly Guid _documentId;
		private readonly Guid _sheetId;
		private readonly Guid _objectId;

		public CadReferenceContactStorageTests()
		{
			_path = Path.Combine(_directory, "Project.mdbx");

			_mdbx = new DataAccessInitializer().InitializeMdbx(_path);

			_database = new DatabaseAccess(
				DatabaseConnectionStrings.Create(_path));

			_read = new Read(_database);

			var projectId = Guid.NewGuid();

			var document = new Document
			{
				ProjectId = projectId,
				Name = "Test.dwg",
				Path = "Test.dwg"
			};

			var sheet = new Sheet
			{
				DocumentId = document.Id,
				SheetNo = "001"
			};

			var entity = new EngineeringObject
			{
				ProjectId = projectId,
				LocationId = Guid.NewGuid()
			};

			_documentId = document.Id;
			_sheetId = sheet.Id;
			_objectId = entity.Id;

			// Seed only the records required by symbol-link reference validation.
			_database.Execute(uow =>
			{
				uow.Save(document);
				uow.Save(sheet);
				uow.Save(entity);
				uow.CommitChanges();
			});
		}

		private ICadSymbolLink AddSymbol(long handle)
		{
			var result = _mdbx.CadSymbolLink.Create(
				_documentId,
				_sheetId,
				handle,
				_objectId);

			Assert.True(
				result.Validation.IsValid,
				string.Join("; ", result.Validation.Errors));

			return result.Value!;
		}

		private Guid AddReference(ICadSymbolLink symbol, params (string Tag, string Value)[] slots)
		{
			return _database.Execute(uow =>
			{
				var contact = new CadReferenceContact
				{
					CadSymbolLinkId = symbol.Id,
					BlockName = "EPCBI05C",
					Designation = "Q02",
					Placement = "+AS1.B3",
					ReferenceText = @"Power.dwg\002 (3.7)"
				};

				uow.Save(contact);

				foreach (var slot in slots)
				{
					uow.Save(new CadReferenceContactTerminal
					{
						ReferenceContactId = contact.Id,
						Tag = slot.Tag,
						Value = slot.Value
					});
				}

				uow.CommitChanges();

				return contact.Id;
			});
		}

		[Fact(DisplayName =	"Reference attributes survive closing the database session")]
		public void Attributes_RoundTrip()
		{
			var symbol = AddSymbol(0x592);

			var id = AddReference(
				symbol,
				("UTTAG1", "13"),
				("UTTAG2", "14"));

			var stored = _read.GetById<CadReferenceContact>(id)!;

			Assert.Equal(symbol.Id, stored.CadSymbolLinkId);
			Assert.Equal("Q02", stored.Designation);
			Assert.Equal("+AS1.B3", stored.Placement);
			Assert.Equal(@"Power.dwg\002 (3.7)", stored.ReferenceText);
			Assert.NotNull(stored.LoadedVersion);

			var slots = _read.GetAll<CadReferenceContactTerminal>(
				x => x.ReferenceContactId == id);

			Assert.Equal(
				new[] { "13", "14" },
				slots.OrderBy(x => x.Tag).Select(x => x.Value));
			Assert.Equal("EPCBI05C", stored.BlockName);
		}

		[Fact(DisplayName =	"Reference contacts have no six-slot limit")]
		public void MoreThanSixSlots_ArePersisted()
		{
			var values = Enumerable.Range(1, 12)
				.Select(n => ("UTTAG" + n, "T" + n))
				.ToArray();

			var id = AddReference(AddSymbol(1), values);

			Assert.Equal(
				12,
				_read.GetAll<CadReferenceContactTerminal>(
					x => x.ReferenceContactId == id).Count);
		}

		[Fact(DisplayName =	"A symbol link can have only one reference-contact record")]
		public void DuplicateContact_IsRejectedByIndex()
		{
			var symbol = AddSymbol(1);
			AddReference(symbol);

			var error = Assert.ThrowsAny<Exception>(
				() => AddReference(symbol));

			Assert.True(VistaDbErrors.IsDuplicateKey(error));
			Assert.Single(_read.GetAll<CadReferenceContact>());
		}

		[Fact(DisplayName =	"Duplicate reference attribute tags roll back the entire insert")]
		public void DuplicateTag_IsRejectedByIndex()
		{
			var symbol = AddSymbol(1);

			var error = Assert.ThrowsAny<Exception>(() =>
				AddReference(
					symbol,
					("UTTAG1", "13"),
					("UTTAG1", "14")));

			Assert.True(VistaDbErrors.IsDuplicateKey(error));
			Assert.Empty(_read.GetAll<CadReferenceContact>());
			Assert.Empty(_read.GetAll<CadReferenceContactTerminal>());
		}

		[Fact(DisplayName =	"Multiple reference contacts can display the same object's terminal")]
		public void SameValueOnDifferentReferences_IsAllowed()
		{
			AddReference(AddSymbol(1), ("UTTAG1", "13"));
			AddReference(AddSymbol(2), ("UTTAG1", "13"));

			Assert.Equal(
				2,
				_read.GetAll<CadReferenceContactTerminal>().Count);

			Assert.Empty(
				_mdbx.CadSymbolLink.GetObjectTerminals(_objectId));
		}

		[Fact(DisplayName =	"Reference attributes do not occupy connectable object terminals")]
		public void Reference_DoesNotBlockPhysicalTerminal()
		{
			var reference = AddSymbol(0x592);

			AddReference(
				reference,
				("UTTAG1", "13"),
				("UTTAG2", "14"));

			var physical = AddSymbol(0x510);

			var result = _mdbx.CadSymbolLink.ReplaceTerminals(
				physical,
				new[]
				{
					("UTTAG1", "13"),
					("UTTAG2", "14")
				});

			Assert.True(
				result.Validation.IsValid,
				string.Join("; ", result.Validation.Errors));

			Assert.Equal(
				2,
				_mdbx.CadSymbolLink.GetTerminals(physical.Id).Count);

			Assert.Empty(
				_mdbx.CadSymbolLink.GetTerminals(reference.Id));

			Assert.Equal(
				2,
				_read.GetAll<CadReferenceContactTerminal>().Count);
		}

		[Fact(DisplayName =	"MDBX reinitialization preserves reference contacts and their attribute slots")]
		public void Reinitialize_PreservesReferences()
		{
			var id = AddReference(
				AddSymbol(1),
				("UTTAG1", "13"));

			new DataAccessInitializer().InitializeMdbx(_path);

			Assert.NotNull(
				_read.GetById<CadReferenceContact>(id));

			Assert.Single(
				_read.GetAll<CadReferenceContactTerminal>(
					x => x.ReferenceContactId == id));
		}

		[Fact(DisplayName =	"Detaching a reference symbol retains its data and engineering object")]
		public void Detach_PreservesReferenceData()
		{
			var symbol = AddSymbol(1);
			var id = AddReference(symbol, ("UTTAG1", "13"));

			Assert.True(
				_mdbx.CadSymbolLink.Delete(symbol).IsValid);

			Assert.NotNull(
				_read.GetById<CadReferenceContact>(id));

			Assert.Single(
				_read.GetAll<CadReferenceContactTerminal>());

			Assert.NotNull(
				_read.GetById<EngineeringObject>(_objectId));

			Assert.True(
				_mdbx.CadSymbolLink
					.GetByBlock(_documentId, symbol.BlockHandle)!
					.IsDetached);
		}

		[Fact(DisplayName = "Reference import stores its block name without occupying physical terminals")]
		public void Import_ReferenceAndPhysicalContact_CanShareTerminal()
		{
			var reference = _mdbx.CadSymbolLink.CreateReferenceContact(
				_documentId,
				_sheetId,
				0x592,
				_objectId,
				"EPCBI05C",
				"Q02",
				"+AS1",
				"old xref",
				new[] { ("UTTAG1", "13") });

			Assert.True(
				reference.Validation.IsValid,
				string.Join("; ", reference.Validation.Errors));

			var stored = Assert.Single(_read.GetAll<CadReferenceContact>());

			Assert.Equal(reference.Value!.Id, stored.CadSymbolLinkId);
			Assert.Equal("EPCBI05C", stored.BlockName);
			Assert.Equal("Q02", stored.Designation);
			Assert.Equal("+AS1", stored.Placement);
			Assert.Equal("old xref", stored.ReferenceText);

			Assert.Empty(
				_mdbx.CadSymbolLink.GetTerminals(reference.Value.Id));

			var physical = _mdbx.CadSymbolLink.CreateWithTerminals(
				_documentId,
				_sheetId,
				0x510,
				_objectId,
				new[] { ("UTTAG1", "13") });

			Assert.True(
				physical.Validation.IsValid,
				string.Join("; ", physical.Validation.Errors));
		}

		[Fact(DisplayName = "Reference snapshots remain readable after their database session has closed")]
		public void ReadReference_ReturnsStoredSnapshot()
		{
			var symbol = AddSymbol(0x592);
			var id = AddReference(
				symbol,
				("UTTAG2", "14"),
				("UTTAG1", "13"));

			var stored = _mdbx.CadSymbolLink.GetReferenceContact(symbol.Id);

			Assert.NotNull(stored);
			Assert.Equal(id, stored!.Id);
			Assert.Equal("EPCBI05C", stored.BlockName);
			Assert.Equal("Q02", stored.Designation);
			Assert.Equal("+AS1.B3", stored.Placement);
			Assert.Equal(@"Power.dwg\002 (3.7)", stored.ReferenceText);
			Assert.NotNull(stored.LoadedVersion);

			var slots = _mdbx.CadSymbolLink.GetReferenceTerminals(symbol.Id);

			Assert.Equal(
				new[] { "UTTAG1", "UTTAG2" },
				slots.Select(slot => slot.Tag));

			Assert.Equal(
				new[] { "13", "14" },
				slots.Select(slot => slot.Value));

			Assert.All(slots, slot => Assert.NotNull(slot.LoadedVersion));
		}

		[Fact(DisplayName =	"Reference reads distinguish symbols representing the same engineering object")]
		public void ReadReference_IsScopedToSymbolLink()
		{
			var first = AddSymbol(1);
			var second = AddSymbol(2);

			var firstId = AddReference(first, ("UTTAG1", "13"));
			AddReference(second, ("UTTAG1", "21"));

			Assert.Equal(
				firstId,
				_mdbx.CadSymbolLink.GetReferenceContact(first.Id)!.Id);

			Assert.Equal(
				"13",
				Assert.Single(
					_mdbx.CadSymbolLink.GetReferenceTerminals(first.Id)).Value);

			Assert.Equal(
				"21",
				Assert.Single(
					_mdbx.CadSymbolLink.GetReferenceTerminals(second.Id)).Value);
		}

		[Fact(DisplayName =	"Unknown links and non-reference symbols have no reference snapshot")]
		public void ReadReference_Missing_ReturnsEmpty()
		{
			foreach (var id in new[] { Guid.NewGuid(), AddSymbol(1).Id })
			{
				Assert.Null(_mdbx.CadSymbolLink.GetReferenceContact(id));
				Assert.Empty(_mdbx.CadSymbolLink.GetReferenceTerminals(id));
			}
		}

		[Fact(DisplayName =	"Detached reference snapshots remain readable for restoration")]
		public void ReadReference_Detached_RemainsAvailable()
		{
			var symbol = AddSymbol(1);
			var id = AddReference(symbol, ("UTTAG1", "13"));

			Assert.True(_mdbx.CadSymbolLink.Delete(symbol).IsValid);

			Assert.Equal(
				id,
				_mdbx.CadSymbolLink.GetReferenceContact(symbol.Id)!.Id);

			Assert.Equal(
				"13",
				Assert.Single(
					_mdbx.CadSymbolLink.GetReferenceTerminals(symbol.Id)).Value);
		}

		[Fact(DisplayName = "Symbol positions are stored per presentation and survive reinitialization")]
		public void Position_RoundTrip_PerSymbol()
		{
			var a = _mdbx.CadSymbolLink.CreateWithTerminals(
				_documentId, _sheetId, 1, _objectId,
				Array.Empty<(string Tag, string Designation)>(),
				(10, 20, 3, "4", "C"));

			var b = _mdbx.CadSymbolLink.CreateWithTerminals(
				_documentId, _sheetId, 2, _objectId,
				Array.Empty<(string Tag, string Designation)>(),
				(90, 80, 0, null, null));

			Assert.True(a.Validation.IsValid);
			Assert.True(b.Validation.IsValid);

			var reopened = new DataAccessInitializer().InitializeMdbx(_path);
			var stored = reopened.CadSymbolLink.GetByBlock(_documentId, 1)!;

			Assert.Equal(10d, stored.PositionX);
			Assert.Equal(20d, stored.PositionY);
			Assert.Equal(3d, stored.PositionZ);
			Assert.Equal("4", stored.GridColumn);
			Assert.Equal("C", stored.GridRow);

			var other = reopened.CadSymbolLink.GetByBlock(_documentId, 2)!;

			Assert.Equal(90d, other.PositionX);
			Assert.Null(other.GridColumn);
			Assert.Null(other.GridRow);
		}

		[Fact(DisplayName =	"A new analysis clears obsolete grid labels while retaining the current CAD position")]
		public void Position_Update_ClearsGrid()
		{
			var created = _mdbx.CadSymbolLink.CreateWithTerminals(
				_documentId, _sheetId, 1, _objectId,
				new[] { ("UTTAG1", "A1") },
				(10, 20, 0, "1", "A"));

			Assert.True(created.Validation.IsValid);

			var changed = _mdbx.CadSymbolLink.ReplaceTerminals(
				created.Value!,
				new[] { ("UTTAG1", "A1") },
				(300, 400, 5, null, null));

			Assert.True(changed.Validation.IsValid);

			var stored = _mdbx.CadSymbolLink.GetByBlock(_documentId, 1)!;

			Assert.Equal(300d, stored.PositionX);
			Assert.Equal(400d, stored.PositionY);
			Assert.Equal(5d, stored.PositionZ);
			Assert.Null(stored.GridColumn);
			Assert.Null(stored.GridRow);
		}

		[Fact(DisplayName =	"Reference updates preserve omitted positions and accept explicit new positions")]
		public void Position_Reference_PreservesAndUpdates()
		{
			var created = _mdbx.CadSymbolLink.CreateReferenceContact(
				_documentId, _sheetId, 1, _objectId,
				"EPCBI05C", "Q02", "", "",
				new[] { ("UTTAG1", "13") },
				(10, 20, 0, "2", "B"));

			Assert.True(created.Validation.IsValid);

			var retained = _mdbx.CadSymbolLink.ReplaceReferenceContact(
				created.Value!,
				"EPCBI05C", "Q02", "", "New text",
				new[] { ("UTTAG1", "13") });

			Assert.True(retained.Validation.IsValid);
			Assert.Equal("2", retained.Value!.GridColumn);
			Assert.Equal(10d, retained.Value.PositionX);

			var moved = _mdbx.CadSymbolLink.ReplaceReferenceContact(
				retained.Value,
				"EPCBI05C", "Q02", "", "New text",
				new[] { ("UTTAG1", "13") },
				(50, 60, 0, null, null));

			Assert.True(moved.Validation.IsValid);

			var stored = _mdbx.CadSymbolLink.GetByBlock(_documentId, 1)!;

			Assert.Equal(50d, stored.PositionX);
			Assert.Null(stored.GridColumn);
			Assert.Null(stored.GridRow);
		}

		[Fact(DisplayName =	"Invalid position data leaves stored coordinates and versions unchanged")]
		public void Position_Invalid_IsRejected()
		{
			var created = _mdbx.CadSymbolLink.CreateWithTerminals(
				_documentId, _sheetId, 1, _objectId,
				Array.Empty<(string Tag, string Designation)>(),
				(10, 20, 0, "1", "A"));

			Assert.True(created.Validation.IsValid);

			var invalid =
				new (double X, double Y, double Z, string? Column, string? Row)[]
				{
			(double.NaN, 20, 0, null, null),
			(10, double.PositiveInfinity, 0, null, null),
			(10, 20, 0, "1", null),
			(10, 20, 0, " ", "A"),
			(10, 20, 0, new string('X', 256), "A")
				};

			foreach (var position in invalid)
			{
				var result = _mdbx.CadSymbolLink.ReplaceTerminals(
					created.Value!,
					Array.Empty<(string Tag, string Designation)>(),
					position);

				Assert.False(result.Validation.IsValid);

				var stored = _mdbx.CadSymbolLink.GetByBlock(_documentId, 1)!;

				Assert.Equal(10d, stored.PositionX);
				Assert.Equal("1", stored.GridColumn);
				Assert.Equal(created.Value!.LoadedVersion, stored.LoadedVersion);
			}
		}

		[Fact(DisplayName =	"A stale position update cannot overwrite the latest analysis")]
		public void Position_StaleVersion_IsRejected()
		{
			var original = AddSymbol(1);

			Assert.Null(original.PositionX);

			var first = _mdbx.CadSymbolLink.ReplaceTerminals(
				original,
				Array.Empty<(string Tag, string Designation)>(),
				(10, 20, 0, "1", "A"));

			Assert.True(first.Validation.IsValid);

			var second = _mdbx.CadSymbolLink.ReplaceTerminals(
				original,
				Array.Empty<(string Tag, string Designation)>(),
				(90, 80, 0, "9", "Z"));

			Assert.False(second.Validation.IsValid);

			var stored = _mdbx.CadSymbolLink.GetByBlock(_documentId, 1)!;

			Assert.Equal(10d, stored.PositionX);
			Assert.Equal("1", stored.GridColumn);
		}

		[Fact(DisplayName =	"A physical contact resolves reference contacts across documents by terminal value")]
		public void XTargets_PhysicalToReference_UsesStoredSheetAndCoordinate()
		{
			var physical = _mdbx.CadSymbolLink.CreateWithTerminals(
				_documentId,
				_sheetId,
				1,
				_objectId,
				new[] { ("UTTAG1", "1"), ("UTTAG2", "2") });

			Assert.True(physical.Validation.IsValid);

			var document = new Document
			{
				ProjectId = _read.GetById<Document>(_documentId)!.ProjectId,
				Name = "Other.dwg",
				Path = "Other.dwg"
			};

			var sheet = new Sheet
			{
				DocumentId = document.Id,
				SheetNo = "002"
			};

			_database.Execute(uow =>
			{
				uow.Save(document);
				uow.Save(sheet);
				uow.CommitChanges();
			});

			var reference = _mdbx.CadSymbolLink.CreateReferenceContact(
				document.Id,
				sheet.Id,
				2,
				_objectId,
				"EPCBI05C",
				"Q03",
				"",
				"",
				new[] { ("UTTAG5", "1"), ("UTTAG6", "2") },
				(10, 20, 0, "4", "C"));

			Assert.True(reference.Validation.IsValid);

			var target = Assert.Single(
				_mdbx.CadSymbolLink.GetReferenceTargets(physical.Value!.Id));

			Assert.Equal(document.Id, target.DocumentId);
			Assert.Equal("Other.dwg", target.DocumentName);
			Assert.Equal("002", target.SheetNo);
			Assert.Equal("4", target.Column);
			Assert.Equal("C", target.Row);

			Assert.True(
				_mdbx.CadSymbolLink.Delete(reference.Value!).IsValid);

			Assert.Empty(
				_mdbx.CadSymbolLink.GetReferenceTargets(physical.Value.Id));
		}

		[Fact(DisplayName =	"Renumbering a sheet updates both reference directions without replacing symbol links")]
		public void XTargets_SheetRenumbered_PreservesLinks()
		{
			var reference = _mdbx.CadSymbolLink.CreateReferenceContact(
				_documentId, _sheetId, 1, _objectId,
				"EPCBI05C", "Q03", "", "",
				new[] { ("UTTAG1", "1") },
				(10, 20, 0, "4", "C"));

			var physical = _mdbx.CadSymbolLink.CreateWithTerminals(
				_documentId, _sheetId, 2, _objectId,
				new[] { ("UTTAG2", "1") },
				(50, 60, 0, "2", "D"));

			Assert.True(reference.Validation.IsValid);
			Assert.True(physical.Validation.IsValid);

			Assert.Equal("001", Assert.Single(
				_mdbx.CadSymbolLink.GetReferenceTargets(reference.Value!.Id)).SheetNo);

			Assert.Equal("001", Assert.Single(
				_mdbx.CadSymbolLink.GetReferenceTargets(physical.Value!.Id)).SheetNo);

			var sheet = _mdbx.Sheet.GetById(_sheetId)!;
			sheet.SheetNo = "002";

			var updated = _mdbx.Sheet.Update(sheet);

			Assert.True(updated.Validation.IsValid);
			Assert.Equal(_sheetId, updated.Value!.Id);

			var forward = Assert.Single(
				_mdbx.CadSymbolLink.GetReferenceTargets(reference.Value.Id));

			var reverse = Assert.Single(
				_mdbx.CadSymbolLink.GetReferenceTargets(physical.Value.Id));

			Assert.Equal("002", forward.SheetNo);
			Assert.Equal("002", reverse.SheetNo);
			Assert.Equal("2", forward.Column);
			Assert.Equal("D", forward.Row);
			Assert.Equal("4", reverse.Column);
			Assert.Equal("C", reverse.Row);

			foreach (var original in new[] { reference.Value, physical.Value })
			{
				var stored = _mdbx.CadSymbolLink.GetByBlock(
					_documentId,
					original.BlockHandle)!;

				Assert.Equal(original.Id, stored.Id);
				Assert.Equal(_sheetId, stored.SheetId);
				Assert.Equal(_objectId, stored.EngineeringObjectId);
				Assert.False(stored.IsDetached);
			}
		}

		public void Dispose()
		{
			// Delete only the unique temporary directory created by this fixture.
			if (Directory.Exists(_directory))
				Directory.Delete(_directory, true);
		}
	}
}