using ELPROCAD.DataAccess.Contracts.MDBX.Documents;
using FluentValidation.Results;

namespace ELPROCAD.DataAccess.Repositories.MDBX.Connections
{
	public interface IConnectionAnalysisRepository
	{
		/// <summary>
		/// Replaces the analyzed local nets and terminal memberships of one sheet atomically.
		/// Include every physical terminal, including unconnected terminals (null NetworkNumber).
		/// SymbolVersion must be captured with the terminal mapping used by the caller.
		/// This stores connectivity, not conductor routing or wiring order.
		/// </summary>
		ValidationResult Replace(
			ISheet sheet,
			IReadOnlyList<int> networkNumbers,
			IReadOnlyList<(Guid SymbolId, int SymbolVersion, string Tag,
				Guid TerminalId, int? NetworkNumber)> terminals);
	}
}
