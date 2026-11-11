using DevExpress.Xpo;
using Microsoft.Extensions.DependencyInjection;

namespace ELPROCAD.CAD.Data.Provider
{
	public sealed class DatabaseAccess
	{
		private readonly IServiceScopeFactory _scopeFactory;

		public DatabaseAccess(IServiceScopeFactory serviceScopeFactory) => 
			_scopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));

		public TResult Execute<TResult>(Func<UnitOfWork, TResult> operation)
		{
			if (operation == null) 
				throw new ArgumentNullException(nameof(operation));

			using var scope = _scopeFactory.CreateScope();

			var uow = scope.ServiceProvider.GetRequiredService<UnitOfWork>();

			uow.LockingOption = LockingOption.Optimistic;

			return operation(uow);
		}
	}
}
