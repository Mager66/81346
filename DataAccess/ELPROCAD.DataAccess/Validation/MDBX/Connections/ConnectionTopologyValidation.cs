using DevExpress.Xpo;
using ELPROCAD.DataAccess.Models.MDBX.Connections;
using ELPROCAD.DataAccess.Models.MDBX.Documents;
using ELPROCAD.DataAccess.Models.MDBX.Objects;
using ELPROCAD.DataAccess.Models.MDBX.References;
using ELPROCAD.DataAccess.Models.MDBX.Terminals;
using FluentValidation.Results;

namespace ELPROCAD.DataAccess.Validation.MDBX.Connections
{
	/// <summary>
	/// Structural validation only. Does not infer links, terminal-side behavior or numbering policies.
	/// A transactional writer must call these checks and protect its dependencies before committing.
	/// </summary>
	internal static class ConnectionTopologyValidation
	{
		internal static ValidationResult Validate(UnitOfWork uow, ElectricalPotential value)
		{
			// Project existence in PDSX is checked by the coordinating application service.
			if (value.ProjectId == Guid.Empty)
				return ValidationResults.Failure(nameof(value.ProjectId), 
					"A project identifier is required.");

			return !string.IsNullOrWhiteSpace(value.Name) && value.Name.Length <= 255
				? new ValidationResult()
				: ValidationResults.Failure(nameof(value.Name), 
				"A potential name containing 1 to 255 characters is required.");
		}

		internal static ValidationResult Validate(UnitOfWork uow, ConnectionNetwork value)
		{
			if (value.Number <= 0)
				return ValidationResults.Failure(nameof(value.Number), 
					"A positive local network number is required.");

			return ProjectId(uow, value.SheetId).HasValue
				? new ValidationResult()
				: ValidationResults.Failure(nameof(value.SheetId), 
				"The sheet and its document must exist.");
		}

		internal static ValidationResult Validate(UnitOfWork uow, ConnectionPoint value)
		{
			var projectId = ProjectId(uow, value.SheetId);
			if (!projectId.HasValue)
				return ValidationResults.Failure(nameof(value.SheetId), "The sheet and its document must exist.");

			if (!Enum.IsDefined(typeof(ConnectionPointKind), value.Kind))
				return ValidationResults.Failure(nameof(value.Kind), "A supported connection point kind is required.");

			if (value.Name == null || value.Name.Length > 255)
				return ValidationResults.Failure(nameof(value.Name), "The point name must contain at most 255 characters.");

			if (value.Kind == ConnectionPointKind.Potential)
			{
				var potential = value.ElectricalPotentialId.HasValue
					? uow.GetObjectByKey<ElectricalPotential>(value.ElectricalPotentialId.Value) : null;

				if (potential == null || potential.ProjectId != projectId.Value || value.EngineeringObjectTerminalId.HasValue)
					return ValidationResults.Failure(nameof(value.ElectricalPotentialId), "A potential point requires a potential in this project and must not reference an apparatus terminal.");
			}
			else if (value.ElectricalPotentialId.HasValue)
			{ 
				return ValidationResults.Failure(nameof(value.ElectricalPotentialId), "Only potential points can reference an electrical potential.");
			}

			if (value.CadSymbolLinkId == Guid.Empty || value.EngineeringObjectTerminalId == Guid.Empty)
				return ValidationResults.Failure("", "Optional identifiers must be null or non-empty GUIDs.");

			if ((value.Kind == ConnectionPointKind.ApparatusTerminal || value.Kind == ConnectionPointKind.TerminalSide)
				&& !value.EngineeringObjectTerminalId.HasValue)
				return ValidationResults.Failure(nameof(value.EngineeringObjectTerminalId), "A terminal reference is required.");

			if (value.EngineeringObjectTerminalId.HasValue && !value.CadSymbolLinkId.HasValue)
				return ValidationResults.Failure(nameof(value.CadSymbolLinkId), "A terminal point requires its CAD symbol link.");

			if (!value.CadSymbolLinkId.HasValue) 
				return new ValidationResult();

			var symbol = uow.GetObjectByKey<CadSymbolLink>(value.CadSymbolLinkId.Value);
			var sheet = uow.GetObjectByKey<Sheet>(value.SheetId);

			if (symbol == null || symbol.IsDetached || symbol.SheetId != value.SheetId || symbol.DocumentId != sheet.DocumentId)
				return ValidationResults.Failure(nameof(value.CadSymbolLinkId), 
					"An active symbol on this sheet is required.");

			var owner = uow.GetObjectByKey<EngineeringObject>(symbol.EngineeringObjectId);

			if (owner == null || owner.IsMarkedForDeletion || owner.ProjectId != projectId.Value)
				return ValidationResults.Failure(nameof(value.CadSymbolLinkId), 
					"The symbol must reference an active object in this project.");

			if (new XPQuery<CadReferenceContact>(uow).Any(x => x.CadSymbolLinkId == symbol.Id))
				return ValidationResults.Failure(nameof(value.CadSymbolLinkId), 
					"A contact-picture reference is not an electrical connection point.");

			if (value.EngineeringObjectTerminalId.HasValue)
			{
				var terminal = uow.GetObjectByKey<EngineeringObjectTerminal>(value.EngineeringObjectTerminalId.Value);
				if (terminal == null || terminal.EngineeringObjectId != owner.Id ||
					!new XPQuery<CadSymbolTerminal>(uow).Any(x => x.CadSymbolLinkId == symbol.Id &&
						x.EngineeringObjectTerminalId == terminal.Id))
					return ValidationResults.Failure(nameof(value.EngineeringObjectTerminalId), "The terminal must be represented by the selected symbol.");
			}
			return new ValidationResult();
		}

		internal static ValidationResult Validate(UnitOfWork uow, ConnectionNetworkPoint value)
		{
			var network = uow.GetObjectByKey<ConnectionNetwork>(value.NetworkId);
			var point = uow.GetObjectByKey<ConnectionPoint>(value.PointId);
			if (network == null || point == null)
				return ValidationResults.Failure("", "The network and connection point must exist.");

			return network.SheetId == point.SheetId
				? new ValidationResult()
				: ValidationResults.Failure(nameof(value.PointId), "A local network can contain points on its own sheet only.");
		}

		internal static ValidationResult Validate(UnitOfWork uow, ConnectionPointLink value)
		{
			if (value.FirstPointId == Guid.Empty || value.SecondPointId == Guid.Empty ||
				value.FirstPointId.CompareTo(value.SecondPointId) >= 0)
				return ValidationResults.Failure("", "Two distinct point identifiers in canonical order are required. Use SetEndpoints.");

			if (!Enum.IsDefined(typeof(ConnectionPointLinkKind), value.Kind))
				return ValidationResults.Failure(nameof(value.Kind), "A supported connection link kind is required.");

			var first = uow.GetObjectByKey<ConnectionPoint>(value.FirstPointId);
			var second = uow.GetObjectByKey<ConnectionPoint>(value.SecondPointId);
			if (first == null || second == null)
				return ValidationResults.Failure("", "Both connection points must exist.");

			var projectId = ProjectId(uow, first.SheetId);
			return projectId.HasValue && projectId == ProjectId(uow, second.SheetId)
				? new ValidationResult()
				: ValidationResults.Failure("", "Linked points must belong to the same project.");
		}

		private static Guid? ProjectId(UnitOfWork uow, Guid sheetId)
		{
			var sheet = uow.GetObjectByKey<Sheet>(sheetId);
			return sheet == null ? null : uow.GetObjectByKey<Document>(sheet.DocumentId)?.ProjectId;
		}
	}
}
