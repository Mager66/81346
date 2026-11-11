using DevExpress.Xpo;

namespace ELPROCAD.DataAccess.Database
{
	internal interface IDatabaseAccess
	{
		/// <summary>
		/// Executes an operation within a dedicated unit of work.
		/// Results must be materialized before the operation returns.
		/// </summary>
		TResult Execute<TResult>(Func<UnitOfWork, TResult> operation);

		/// <summary>
		/// Executes an operation within a dedicated unit of work.
		/// Changes are committed explicitly by the operation.
		/// </summary>
		void Execute(Action<UnitOfWork> operation);

		TResult ExecuteExplicit<TResult>(Func<ExplicitUnitOfWork, TResult> operation);
	}
}
