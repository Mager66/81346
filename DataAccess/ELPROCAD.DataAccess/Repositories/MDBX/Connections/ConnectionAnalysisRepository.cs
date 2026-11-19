using DevExpress.Xpo;
using DevExpress.Xpo.DB.Exceptions;
using ELPROCAD.DataAccess.Contracts.MDBX.Documents;
using ELPROCAD.DataAccess.Database;
using ELPROCAD.DataAccess.Models;
using ELPROCAD.DataAccess.Models.MDBX.Connections;
using ELPROCAD.DataAccess.Models.MDBX.Documents;
using ELPROCAD.DataAccess.Models.MDBX.Objects;
using ELPROCAD.DataAccess.Models.MDBX.Terminals;
using ELPROCAD.DataAccess.Validation;
using ELPROCAD.DataAccess.Validation.MDBX.Connections;
using FluentValidation.Results;

namespace ELPROCAD.DataAccess.Repositories.MDBX.Connections
{
	internal sealed class ConnectionAnalysisRepository : IConnectionAnalysisRepository
	{
		private readonly IDatabaseAccess _database;
		private readonly Action? _afterFlush;

		internal ConnectionAnalysisRepository(IDatabaseAccess database, Action? afterFlush = null)
		{
			_database = database ?? throw new ArgumentNullException(nameof(database));
			_afterFlush = afterFlush;
		}

		public ValidationResult Replace(ISheet sheet, 
			IReadOnlyList<int> networkNumbers,
			IReadOnlyList<(Guid SymbolId, int SymbolVersion, string Tag, Guid TerminalId, int? NetworkNumber)> terminals)
		{
			if (sheet == null) 
				throw new ArgumentNullException(nameof(sheet));

			if (networkNumbers == null) 
				throw new ArgumentNullException(nameof(networkNumbers));

			if (terminals == null) 
				throw new ArgumentNullException(nameof(terminals));

			var numbers = networkNumbers.ToArray();
			var inputs = terminals.ToArray();

			if (!sheet.LoadedVersion.HasValue || numbers.Any(x => x <= 0) ||
				numbers.Distinct().Count() != numbers.Length ||
				inputs.Any(x => x.SymbolId == Guid.Empty || x.TerminalId == Guid.Empty ||
					string.IsNullOrWhiteSpace(x.Tag) || x.Tag.Trim().Length > 255 ||
					(x.NetworkNumber.HasValue && !numbers.Contains(x.NetworkNumber.Value))))
				return ValidationResults.Failure("", "The connection snapshot contains invalid identifiers, networks or versions.");

			var normalized = inputs.Select(x => (x.SymbolId, x.SymbolVersion,
				Tag: x.Tag.Trim().ToUpperInvariant(), x.TerminalId, x.NetworkNumber)).ToArray();

			if (normalized.GroupBy(x => (x.SymbolId, x.Tag)).Any(x => x.Count() != 1))
				return ValidationResults.Failure("", "A symbol slot may occur only once in a sheet snapshot.");

			try
			{
				return _database.ExecuteExplicit(uow =>
				{
					var storedSheet = uow.GetObjectByKey<Sheet>(sheet.Id);
					if (storedSheet == null || Concurrency.GetVersion(uow, storedSheet) != sheet.LoadedVersion)
						return ValidationResults.Failure("", "The sheet has changed. Analyze the sheet again.");

					var oldNetworks = new XPQuery<ConnectionNetwork>(uow).Where(x => x.SheetId == sheet.Id && x.IsFromAnalysis).ToList();
					var oldPoints = new XPQuery<ConnectionPoint>(uow).Where(x => x.SheetId == sheet.Id && x.AnalysisTag != "" && x.AnalysisTag != null).ToList();
					var retained = new List<(ConnectionPoint Point, int? Network)>();
					var dependencies = new Dictionary<Guid, VersionedEntity> { [storedSheet.Id] = storedSheet };
					var document = uow.GetObjectByKey<Document>(storedSheet.DocumentId);

					if (document == null)
						return ValidationResults.Failure("", "The document no longer exists.");

					dependencies[document.Id] = document;
					var symbolIds = new XPQuery<CadSymbolLink>(uow)
						.Where(x => x.SheetId == sheet.Id && x.DetachedAtUtc == null).Select(x => x.Id).ToArray();
					var mappings = new XPQuery<CadSymbolTerminal>(uow)
						.Where(x => Enumerable.Contains(symbolIds, x.CadSymbolLinkId)).ToList();
					if (mappings.Count != normalized.Length || mappings.Any(x => !normalized.Any(y =>
						y.SymbolId == x.CadSymbolLinkId && y.TerminalId == x.EngineeringObjectTerminalId &&
						string.Equals(y.Tag, x.Tag, StringComparison.OrdinalIgnoreCase))))
						return ValidationResults.Failure("", "The snapshot must include every active physical terminal on the sheet. Analyze again.");

					foreach (var input in normalized)
					{
						var symbol = uow.GetObjectByKey<CadSymbolLink>(input.SymbolId);
						if (symbol == null || Concurrency.GetVersion(uow, symbol) != input.SymbolVersion)
							return ValidationResults.Failure("", "A symbol has changed. Analyze the sheet again.");

						var mapping = mappings.SingleOrDefault(x =>
							x.CadSymbolLinkId == input.SymbolId &&
							string.Equals(x.Tag, input.Tag, StringComparison.OrdinalIgnoreCase));
						if (mapping == null || mapping.EngineeringObjectTerminalId != input.TerminalId)
							return ValidationResults.Failure("", "A symbol terminal mapping has changed. Analyze the sheet again.");

						var point = oldPoints.SingleOrDefault(x => x.CadSymbolLinkId == input.SymbolId &&
							x.EngineeringObjectTerminalId == input.TerminalId && x.AnalysisTag == input.Tag)
							?? new ConnectionPoint
							{
								SheetId = sheet.Id,
								Kind = ConnectionPointKind.ApparatusTerminal,
								CadSymbolLinkId = input.SymbolId,
								EngineeringObjectTerminalId = input.TerminalId,
								AnalysisTag = input.Tag
							};

						var validation = ConnectionTopologyValidation.Validate(uow, point);

						if (!validation.IsValid)
							return validation;

						retained.Add((point, input.NetworkNumber));
						dependencies[symbol.Id] = symbol;
						var terminal = uow.GetObjectByKey<EngineeringObjectTerminal>(input.TerminalId);
						dependencies[terminal.Id] = terminal;
						var owner = uow.GetObjectByKey<EngineeringObject>(symbol.EngineeringObjectId);
						dependencies[owner.Id] = owner;
					}
					var removed = oldPoints.Where(x => retained.All(y => y.Point.Id != x.Id)).ToList();
					var removedIds = removed.Select(x => x.Id).ToArray();
					if (new XPQuery<ConnectionPointLink>(uow).Any(x =>
						Enumerable.Contains(removedIds, x.FirstPointId) || Enumerable.Contains(removedIds, x.SecondPointId)))
						return ValidationResults.Failure("", "An obsolete analysis point has explicit links. Resolve those links before replacing the analysis.");

					var oldNetworkIds = oldNetworks.Select(x => x.Id).ToArray();
					var oldPointIds = oldPoints.Select(x => x.Id).ToArray();
					var memberships = new XPQuery<ConnectionNetworkPoint>(uow)
						.Where(x => Enumerable.Contains(oldNetworkIds, x.NetworkId) || Enumerable.Contains(oldPointIds, x.PointId)).ToList();
					if (memberships.Any(x => !Enumerable.Contains(oldNetworkIds, x.NetworkId)) ||
						memberships.Any(x => !Enumerable.Contains(oldPointIds, x.PointId)))
						return ValidationResults.Failure("", "The analysis topology has externally assigned memberships. Resolve them before replacing the analysis.");

					foreach (var member in memberships) uow.Delete(member);
					foreach (var point in removed) uow.Delete(point);
					foreach (var network in oldNetworks.Where(x => !numbers.Contains(x.Number))) uow.Delete(network);
					// Flush releases unique membership keys, but does not commit the transaction.
					uow.FlushChanges();
					_afterFlush?.Invoke();

					var networks = new Dictionary<int, ConnectionNetwork>();

					foreach (var number in numbers)
					{
						var network = oldNetworks.SingleOrDefault(x => x.Number == number)
							?? new ConnectionNetwork { SheetId = sheet.Id, Number = number, IsFromAnalysis = true };
						var validation = ConnectionTopologyValidation.Validate(uow, network);
						if (!validation.IsValid) return validation;
						uow.Save(network);
						networks.Add(number, network);
					}
					foreach (var entry in retained)
					{
						uow.Save(entry.Point);
						if (entry.Network.HasValue)
							uow.Save(new ConnectionNetworkPoint
							{ NetworkId = networks[entry.Network.Value].Id, PointId = entry.Point.Id });
					}

					DependencyVersions.Touch(uow, dependencies.Values);
					uow.FlushChanges();
					uow.CommitTransaction();

					return new ValidationResult();
				});
			}
			catch (LockingException)
			{
				return ValidationResults.Failure("", "The connection dependencies changed. No connection changes were saved. Analyze again.");
			}
			catch (Exception ex) when (VistaDbErrors.IsDuplicateKey(ex))
			{
				return ValidationResults.Failure("", "A connection key conflicts with existing data. No connection changes were saved.");
			}
		}
	}
}
