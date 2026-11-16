using ELPROCAD.DataAccess.Contracts.MDBX.Objects;
using ELPROCAD.DataAccess.Contracts.MDBX.References;
using FluentValidation.Results;

namespace ELPROCAD.DataAccess.Repositories.MDBX.Objects
{
	public interface ICadSymbolLinkRepository
	{
		/// <summary>
		/// Finds the registered object association for a block in a document.
		/// </summary>
		ICadSymbolLink? GetByBlock(Guid documentId, long blockHandle);

		/// <summary>
		/// Returns the registered symbol links in a document.
		/// </summary>
		IReadOnlyList<ICadSymbolLink> GetByDocumentId(Guid documentId);

		/// <summary>
		/// Returns all registered symbols referencing an engineering object.
		/// </summary>
		IReadOnlyList<ICadSymbolLink> GetByObjectId(Guid engineeringObjectId);

		/// <summary>
		/// Creates a symbol link after validating its references.
		/// An existing document and handle combination is rejected.
		/// </summary>
		(ICadSymbolLink? Value, ValidationResult Validation) Create(
			Guid documentId,
			Guid sheetId,
			long blockHandle,
			Guid engineeringObjectId);

		/// <summary>
		/// Gets the CAD symbol links belonging to a sheet.
		/// </summary>
		IReadOnlyList<ICadSymbolLink> GetBySheetId(Guid sheetId);

		/// <summary>
		/// Disconnects the symbol while retaining its identity for later reattachment.
		/// </summary>
		ValidationResult Delete(ICadSymbolLink entity);

		/// <summary>
		/// Reactivates an existing disconnected symbol link.
		/// </summary>
		ValidationResult Reattach(ICadSymbolLink entity);

		/// <summary>Reads a symbol's terminal mappings.</summary>
		IReadOnlyList<ICadSymbolTerminal> GetTerminals(Guid symbolLinkId);

		/// <summary>Reads the terminal catalog belonging to an engineering object.</summary>
		IReadOnlyList<IEngineeringObjectTerminal> GetObjectTerminals(Guid engineeringObjectId);

		/// <summary>Atomically replaces the complete terminal list of an active symbol.</summary>
		(ICadSymbolLink? Value, ValidationResult Validation) ReplaceTerminals(
			ICadSymbolLink symbol, IReadOnlyList<(string Tag, string Designation)> terminals,
			(double X, double Y, double Z, string? Column, string? Row)? position = null);

		/// <summary>Creates the link and its complete terminal list atomically.</summary>
		(ICadSymbolLink? Value, ValidationResult Validation) CreateWithTerminals(
			Guid documentId, Guid sheetId, long blockHandle, Guid engineeringObjectId,
			IReadOnlyList<(string Tag, string Designation)> terminals,
			(double X, double Y, double Z, string? Column, string? Row)? position = null);

		/// <summary>
		/// Creates a reference symbol and its attribute snapshot in one commit.
		/// </summary>
		(ICadSymbolLink? Value, ValidationResult Validation) CreateReferenceContact(
			Guid documentId,
			Guid sheetId,
			long blockHandle,
			Guid engineeringObjectId,
			string blockName,
			string designation,
			string placement,
			string referenceText,
			IReadOnlyList<(string Tag, string Designation)> slots, 
			(double X, double Y, double Z, string? Column, string? Row)? position = null);

		/// <summary>
		/// Replaces reference attributes and reactivates the link atomically.
		/// </summary>
		(ICadSymbolLink? Value, ValidationResult Validation) ReplaceReferenceContact(
			ICadSymbolLink symbol,
			string blockName,
			string designation,
			string placement,
			string referenceText,
			IReadOnlyList<(string Tag, string Designation)> slots,
			(double X, double Y, double Z, string? Column, string? Row)? position = null);

		/// <summary>
		/// Reads the reference snapshot, including snapshots for detached links.
		/// </summary>
		ICadReferenceContact? GetReferenceContact(Guid symbolLinkId);

		/// <summary>
		/// Reads reference slots using the CAD symbol link identifier.
		/// </summary>
		IReadOnlyList<ICadReferenceContactTerminal> GetReferenceTerminals(Guid symbolLinkId);

		/// <summary>
		/// Reads persisted target locations matching the reference contact's terminal values.
		/// </summary>
		IReadOnlyList<(
			Guid DocumentId,
			string DocumentName,
			string SheetNo,
			string? Column,
			string? Row)> GetReferenceTargets(Guid symbolLinkId);
	}
}

