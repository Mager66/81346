using ELPROCAD.DataAccess.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ELPROCAD.DataAccess
{
	/// <summary>
	/// Registers the public data access entry points.
	/// </summary>
	public static class DependencyInjection
	{
		public static IServiceCollection AddElprocadDataAccess(
			this IServiceCollection services)
		{
			if (services == null)
				throw new ArgumentNullException(nameof(services));

			services.TryAddSingleton<IDataAccessInitializer>(_ => new DataAccessInitializer());

			services.TryAddSingleton<IValidationService>(_new => new ValidationService());

			return services;
		}
	}
}
