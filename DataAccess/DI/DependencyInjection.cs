using DevExpress.Xpo;
using ELPROCAD.API.Provider;

namespace ELPROCAD.API.DI
{
	public static class DependencyInjection
	{
		public static IServiceCollection AddElprocadDataAccess(this  IServiceCollection services, string connectionString)
		{
			if (string.IsNullOrWhiteSpace(connectionString))
				throw new ArgumentException("En anslutningssträng måste anges.", nameof(connectionString));

			services.AddScoped<IDataLayer>(_ => XpoDataLayerFactory.Create(connectionString));

			services.AddScoped<UnitOfWork>(provider => new UnitOfWork(provider.GetRequiredService<IDataLayer>()));

			services.AddScoped<DatabaseInitializer>();

			services.AddScoped<DatabaseTestData>();

			services.AddScoped<DatabaseTestVerifier>();

			return services;
		}
	}
}