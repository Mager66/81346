using DevExpress.Xpo;
using ELPROCAD.CAD.Data.Analysis;
using ELPROCAD.CAD.Data.CRUD;
using ELPROCAD.CAD.Data.CRUD.Analisys;
using ELPROCAD.CAD.Data.CRUD.Classification;
using ELPROCAD.CAD.Data.Provider;
using Microsoft.Extensions.DependencyInjection;

namespace ELPROCAD.CAD.Data
{
	public static class BootStrap
	{
		public static IServiceCollection AddElprocadDataAccess(this IServiceCollection services, string connectionString)
		{
			if (string.IsNullOrWhiteSpace(connectionString))
				throw new ArgumentException("En anslutningssträng måste anges.", nameof(connectionString));

			services.AddScoped<IDataLayer>(_ => XpoDataLayerFactory.Create(connectionString));

			services.AddScoped<UnitOfWork>(provider => new UnitOfWork(provider.GetRequiredService<IDataLayer>()));
			services.AddScoped<DatabaseInitializer>();

			services.AddScoped<QueryProject>();

			services.AddScoped<QueryEngineeringObject>();
			services.AddScoped<AnalysisBlockImporter>();

			services.AddSingleton<DatabaseAccess>();
			services.AddTransient<QueryMainClassification>();

			services.AddTransient<Read>();

			return services;
		}
	}
}
