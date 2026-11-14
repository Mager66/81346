using DevExpress.Xpo;
using ELPROCAD.DataAccess.Contracts.MDBX.Objects;
using ELPROCAD.DataAccess.Contracts.MDBX.References;
using ELPROCAD.DataAccess.CRUD;
using ELPROCAD.DataAccess.CRUD.Terminals;
using ELPROCAD.DataAccess.Database;
using ELPROCAD.DataAccess.Models;
using ELPROCAD.DataAccess.Models.MDBX.Documents;
using ELPROCAD.DataAccess.Models.MDBX.Objects;
using ELPROCAD.DataAccess.Models.MDBX.References;
using ELPROCAD.DataAccess.Models.MDBX.Terminals;
using ELPROCAD.DataAccess.Validation;
using ELPROCAD.DataAccess.Validation.MDBX.Objects;
using FluentValidation.Results;

namespace ELPROCAD.DataAccess.Repositories.MDBX.Objects
{
	internal sealed class CadSymbolLinkRepository : ICadSymbolLinkRepository
	{
		private readonly Read _read;
		private readonly Create _create;
		private readonly Update _update;
		private readonly CadSymbolLinkValidator _validator = new();

		internal CadSymbolLinkRepository(Read read, Create create, Update update)
		{
			_read = read ?? throw new ArgumentNullException(nameof(read));
			_create = create ?? throw new ArgumentNullException(nameof(create));
			_update = update ?? throw new ArgumentNullException(nameof(update));
		}

		public ICadSymbolLink? GetByBlock(Guid documentId, long blockHandle)
		{
			return _read.GetAll<CadSymbolLink>(entity =>
					entity.DocumentId == documentId &&
					entity.BlockHandle == blockHandle)
				.SingleOrDefault();
		}

		public IReadOnlyList<ICadSymbolLink> GetByDocumentId(Guid documentId)
		{
			return _read.GetAll<CadSymbolLink>(
				entity =>
					entity.DocumentId == documentId &&
					entity.DetachedAtUtc == null);
		}

		public IReadOnlyList<ICadSymbolLink> GetBySheetId(Guid sheetId)
		{
			return _read.GetAll<CadSymbolLink>(
				entity =>
					entity.SheetId == sheetId &&
					entity.DetachedAtUtc == null);
		}

		public IReadOnlyList<ICadSymbolLink> GetByObjectId(Guid engineeringObjectId)
		{
			return _read.GetAll<CadSymbolLink>(
				entity =>
					entity.EngineeringObjectId == engineeringObjectId &&
					entity.DetachedAtUtc == null);
		}

		public (ICadSymbolLink? Value, ValidationResult Validation) Create(
			Guid documentId,
			Guid sheetId,
			long blockHandle,
			Guid engineeringObjectId)
		{
			var input = new CadSymbolLink
			{
				DocumentId = documentId,
				SheetId = sheetId,
				BlockHandle = blockHandle,
				EngineeringObjectId = engineeringObjectId
			};

			var result = _create.New(
				input,
				entity => _validator.Validate(entity),
				CopyLink,
				ValidateReferences);

			return (result.Value, result.Validation);
		}

		public ValidationResult Delete(ICadSymbolLink entity) => SetDetachedState(entity, detached: true);

		public ValidationResult Reattach(ICadSymbolLink entity) => SetDetachedState(entity, detached: false);

		private ValidationResult SetDetachedState(ICadSymbolLink entity, bool detached)
		{
			if (entity == null)
				throw new ArgumentNullException(nameof(entity));

			// Use stored references, not values supplied by the caller.
			var input = new CadSymbolLink
			{
				Id = entity.Id,
				LoadedVersion = entity.LoadedVersion
			};

			var result = _update.Save(
				input,
				_ => new ValidationResult(),
				(unitOfWork, source, target) =>
				{
					target.DetachedAtUtc = detached
						? DateTime.UtcNow
						: (DateTime?)null;
				},
				(unitOfWork, existing) =>
				{
					if (existing.IsDetached == detached)
					{
						return ValidationResults.Failure(
							nameof(ICadSymbolLink.IsDetached),
							detached
								? "The symbol link is already disconnected."
								: "The symbol link is already active.");
					}

					if (detached)
						return new ValidationResult();

					// Reattachment requires valid references and an active object.
					var references = ValidateReferences(unitOfWork, existing);
					return references.IsValid
						? SymbolTerminalReplacement.ValidateReattachment(unitOfWork, existing)
						: references;
				});

			return result.Validation;
		}

		private static ValidationResult ValidateReferences(UnitOfWork uow, CadSymbolLink symbolLink)
		{
			var document = uow.GetObjectByKey<Document>(symbolLink.DocumentId);

			if (document == null)
			{
				return ValidationResults.Failure(
					nameof(symbolLink.DocumentId),
					"The document does not exist.");
			}

			var sheet = uow.GetObjectByKey<Sheet>(symbolLink.SheetId);

			if (sheet == null)
			{
				return ValidationResults.Failure(
					nameof(ICadSymbolLink.SheetId),
					"The sheet does not exist.");
			}

			if (sheet.DocumentId != symbolLink.DocumentId)
			{
				return ValidationResults.Failure(
					nameof(ICadSymbolLink.SheetId),
					"The sheet belongs to another document.");
			}

			var entity = uow.GetObjectByKey<EngineeringObject>(
				symbolLink.EngineeringObjectId);

			if (entity == null)
			{
				return ValidationResults.Failure(
					nameof(symbolLink.EngineeringObjectId),
					"The engineering object does not exist.");
			}

			if (entity.IsMarkedForDeletion)
			{
				return ValidationResults.Failure(
					nameof(symbolLink.EngineeringObjectId),
					"The engineering object is marked for deletion. Restore it before adding symbols.");
			}

			if (document.ProjectId != entity.ProjectId)
			{
				return ValidationResults.Failure(
					nameof(symbolLink.EngineeringObjectId),
					"The document and engineering object must belong to the same project.");
			}

			var duplicate = new XPQuery<CadSymbolLink>(uow)
				.Any(link =>
				link.DocumentId == symbolLink.DocumentId &&
				link.BlockHandle == symbolLink.BlockHandle &&
				link.Id != symbolLink.Id);

			if (duplicate)
			{
				return ValidationResults.Failure(
					nameof(symbolLink.BlockHandle),
					"The block already has a registered engineering object link.");
			}

			// Protect the document, sheet and object against concurrent changes.
			DependencyVersions.Touch(
				uow,
				new VersionedEntity[] { document, sheet, entity });

			return new ValidationResult();
		}

		/// <summary>Reads all registered slots, including unconnected terminals.</summary>
		public IReadOnlyList<ICadSymbolTerminal> GetTerminals(Guid symbolLinkId) =>
			_read.GetAll<CadSymbolTerminal>(x => x.CadSymbolLinkId == symbolLinkId);

		/// <summary>Reads the object's terminal catalog, independent of CAD connections.</summary>
		public IReadOnlyList<IEngineeringObjectTerminal> GetObjectTerminals(Guid engineeringObjectId) =>
			_read.GetAll<EngineeringObjectTerminal>(x => x.EngineeringObjectId == engineeringObjectId);

		/// <summary>
		/// Atomically replaces all slots on one active symbol.
		/// An empty list clears the presentation but retains the object's terminal catalog.
		/// </summary>
		public (ICadSymbolLink? Value, ValidationResult Validation) ReplaceTerminals(
			ICadSymbolLink symbol, IReadOnlyList<(string Tag, string Designation)> terminals)
		{
			if (symbol == null) 
				throw new ArgumentNullException(nameof(symbol));

			if (terminals == null) 
				throw new ArgumentNullException(nameof(terminals));

			var values = terminals.ToArray();

			var input = new CadSymbolLink { Id = symbol.Id, LoadedVersion = symbol.LoadedVersion };

			var replacement = new SymbolTerminalReplacement();

			var result = _update.Save(input,
				_ => SymbolTerminalReplacement.ValidateInput(values),
				(uow, source, target) =>
				{
					replacement.Stage(uow, target);

					// A physical symbol must not retain an obsolete reference snapshot.
					RemoveReferenceContact(uow, target.Id);
				},
				(uow, existing) =>
				{
					var references = ValidateReferences(uow, existing);
					return references.IsValid ? replacement.Prepare(uow, existing, values) : references;
				});
			return (result.Value, result.Validation);
		}

		/// <summary>Creates a symbol and all its terminal mappings in a single commit.</summary>
		public (ICadSymbolLink? Value, ValidationResult Validation) CreateWithTerminals(
			Guid documentId, Guid sheetId, long blockHandle, Guid engineeringObjectId,
			IReadOnlyList<(string Tag, string Designation)> terminals)
		{
			if (terminals == null) throw new ArgumentNullException(nameof(terminals));
			var values = terminals.ToArray();
			var input = new CadSymbolLink
			{
				DocumentId = documentId,
				SheetId = sheetId,
				BlockHandle = blockHandle,
				EngineeringObjectId = engineeringObjectId
			};
			var replacement = new SymbolTerminalReplacement();
			var result = _create.New(input,
				entity => new ValidationResult(_validator.Validate(entity).Errors.Concat(
					SymbolTerminalReplacement.ValidateInput(values).Errors)),
				(uow, source, target) => {
					CopyLink(uow, source, target);
					replacement.Stage(uow, target);
				},
				(uow, entity) => {
					var references = ValidateReferences(uow, entity);
					return references.IsValid ? replacement.Prepare(uow, entity, values) : references;
				});
			return (result.Value, result.Validation);
		}

		public (ICadSymbolLink? Value, ValidationResult Validation) CreateReferenceContact(
	Guid documentId,
	Guid sheetId,
	long blockHandle,
	Guid engineeringObjectId,
	string blockName,
	string designation,
	string placement,
	string referenceText,
	IReadOnlyList<(string Tag, string Designation)> slots)
		{
			return SaveReferenceContact(
				new CadSymbolLink
				{
					DocumentId = documentId,
					SheetId = sheetId,
					BlockHandle = blockHandle,
					EngineeringObjectId = engineeringObjectId
				},
				true,
				blockName,
				designation,
				placement,
				referenceText,
				slots);
		}

		public (ICadSymbolLink? Value, ValidationResult Validation) ReplaceReferenceContact(
			ICadSymbolLink symbol,
			string blockName,
			string designation,
			string placement,
			string referenceText,
			IReadOnlyList<(string Tag, string Designation)> slots)
		{
			if (symbol == null)
				throw new ArgumentNullException(nameof(symbol));

			return SaveReferenceContact(
				new CadSymbolLink
				{
					Id = symbol.Id,
					LoadedVersion = symbol.LoadedVersion,
					DocumentId = symbol.DocumentId,
					SheetId = symbol.SheetId,
					BlockHandle = symbol.BlockHandle,
					EngineeringObjectId = symbol.EngineeringObjectId
				},
				false,
				blockName,
				designation,
				placement,
				referenceText,
				slots);
		}

		private (ICadSymbolLink? Value, ValidationResult Validation) SaveReferenceContact(
			CadSymbolLink input,
			bool isNew,
			string blockName,
			string designation,
			string placement,
			string referenceText,
			IReadOnlyList<(string Tag, string Designation)> slots)
		{
			if (string.IsNullOrWhiteSpace(blockName) || blockName.Length > 255)
			{
				return (null, ValidationResults.Failure(
					nameof(blockName),
					"A block name containing 1 to 255 characters is required."));
			}

			if (designation == null)
				throw new ArgumentNullException(nameof(designation));

			if (placement == null)
				throw new ArgumentNullException(nameof(placement));

			if (referenceText == null)
				throw new ArgumentNullException(nameof(referenceText));

			if (slots == null)
				throw new ArgumentNullException(nameof(slots));

			var values = slots.ToArray();
			var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			foreach (var slot in values)
			{
				if (string.IsNullOrWhiteSpace(slot.Tag) ||
					slot.Tag.Trim().Length > 255 ||
					!tags.Add(slot.Tag.Trim()) ||
					slot.Designation == null ||
					slot.Designation.Length > 255)
				{
					return (null, ValidationResults.Failure(
						nameof(slots),
						"Reference slots require unique non-empty tags " +
						"and values of at most 255 characters."));
				}
			}

			void Copy(UnitOfWork uow, CadSymbolLink source, CadSymbolLink target)
			{
				if (isNew)
					CopyLink(uow, source, target);

				target.DetachedAtUtc = null;

				StageReferenceContact(
					uow, target, blockName, designation,
					placement, referenceText, values);
			}

			var result = isNew
				? _create.New(
					input,
					entity => _validator.Validate(entity),
					Copy,
					ValidateReferences)
				: _update.Save(
					input,
					entity => _validator.Validate(entity),
					Copy,
					ValidateReferences);

			return (result.Value, result.Validation);
		}

		/// <summary>
		/// Stages reference attributes without creating or occupying object terminals.
		/// </summary>
		private static void StageReferenceContact(
			UnitOfWork uow,
			CadSymbolLink symbol,
			string blockName,
			string designation,
			string placement,
			string referenceText,
			IReadOnlyList<(string Tag, string Designation)> slots)
		{
			// Remove physical mappings created by an earlier, incorrect import.
			foreach (var physical in new XPQuery<CadSymbolTerminal>(uow)
				.Where(x => x.CadSymbolLinkId == symbol.Id)
				.ToList())
			{
				uow.Delete(physical);
			}

			var contact = new XPQuery<CadReferenceContact>(uow)
				.SingleOrDefault(x => x.CadSymbolLinkId == symbol.Id)
				?? new CadReferenceContact
				{
					CadSymbolLinkId = symbol.Id
				};

			contact.BlockName = blockName;
			contact.Designation = designation;
			contact.Placement = placement;
			contact.ReferenceText = referenceText;

			uow.Save(contact);

			var remaining = new XPQuery<CadReferenceContactTerminal>(uow)
				.Where(x => x.ReferenceContactId == contact.Id)
				.ToList()
				.ToDictionary(x => x.Tag, StringComparer.OrdinalIgnoreCase);

			foreach (var slot in slots)
			{
				var tag = slot.Tag.Trim();

				if (!remaining.TryGetValue(tag, out var terminal))
				{
					terminal = new CadReferenceContactTerminal
					{
						ReferenceContactId = contact.Id
					};
				}
				else
				{
					remaining.Remove(tag);
				}

				terminal.Tag = tag;
				terminal.Value = slot.Designation;
				uow.Save(terminal);
			}

			foreach (var obsolete in remaining.Values)
				uow.Delete(obsolete);

			// Attribute-only changes must advance the link's concurrency version.
			DependencyVersions.Touch(uow, new[] { symbol });
		}

		private static void RemoveReferenceContact(UnitOfWork uow, Guid symbolLinkId)
		{
			var contact = new XPQuery<CadReferenceContact>(uow)
				.SingleOrDefault(x => x.CadSymbolLinkId == symbolLinkId);

			if (contact == null)
				return;

			foreach (var slot in new XPQuery<CadReferenceContactTerminal>(uow)
				.Where(x => x.ReferenceContactId == contact.Id)
				.ToList())
			{
				uow.Delete(slot);
			}

			uow.Delete(contact);
		}

		private static void CopyLink(
			UnitOfWork uow,
			CadSymbolLink source,
			CadSymbolLink target)
		{
			target.Id = source.Id;
			target.DocumentId = source.DocumentId;
			target.SheetId = source.SheetId;
			target.BlockHandle = source.BlockHandle;
			target.EngineeringObjectId = source.EngineeringObjectId;
		}

		public ICadReferenceContact? GetReferenceContact(Guid symbolLinkId) =>
			_read.GetAll<CadReferenceContact>(contact => contact.CadSymbolLinkId == symbolLinkId)
			.SingleOrDefault();

		public IReadOnlyList<ICadReferenceContactTerminal> GetReferenceTerminals(Guid symbolLinkId)
		{
			var contact = GetReferenceContact(symbolLinkId);

			if (contact == null)
				return Array.Empty<ICadReferenceContactTerminal>();

			return _read.GetAll<CadReferenceContactTerminal>(
					terminal => terminal.ReferenceContactId == contact.Id)
				.OrderBy(terminal => terminal.Tag, StringComparer.OrdinalIgnoreCase)
				.ToList()
				.AsReadOnly();
		}
	}
}

