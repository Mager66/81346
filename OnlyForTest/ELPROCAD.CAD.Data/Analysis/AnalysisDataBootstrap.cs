using ELPROCAD.Analysis.Configuration;
using ELPROCAD.Analysis.Extraction;
using ELPROCAD.CAD.Data.Analysis.Import;
using ELPROCAD.DataAccess;
using ELPROCAD.DataAccess.Repositories.MDBX;
using ELPROCAD.DataAccess.Repositories.PDSX;
using Microsoft.Extensions.DependencyInjection;

namespace ELPROCAD.CAD.Data.Analysis
{
	public static class AnalysisDataBootstrap
	{
		public static IServiceCollection AddAnalysisDataAccess(this IServiceCollection services, string projectDirectory)
		{
			if (services == null)
				throw new ArgumentNullException(nameof(services));

			if (string.IsNullOrWhiteSpace(projectDirectory))
				throw new ArgumentException(
					"A project directory is required.",
					nameof(projectDirectory));

			var directory = Path.GetFullPath(projectDirectory);

			// Validate the explicitly selected project profile before registering database factories.
			var extractor = ExtractionProfiles.LoadElprocadJson(
				File.ReadAllText(Path.Combine(directory, "Extraction.json")));

			services.AddSingleton<IElprocadObjectExtractor>(extractor);

			ELPROCAD.DataAccess.DependencyInjection.AddElprocadDataAccess(services);

			services.AddSingleton<IPdsxRepository>(provider =>
			{
				var initializer = provider.GetRequiredService<IDataAccessInitializer>();

				var repository = initializer.InitializePdsx(Path.Combine(directory, "ELPROCAD.pdsx")); // ToDo: Change to project name

				// Create the project once for this analysis proof of concept
				if (repository.Project.GetAll().Count == 0)
				{
					var result = repository.Project.Create(
						"TEST-81346",
						directory,
						"CAD location analysis test");

					if (!result.Validation.IsValid)
					{
						throw new InvalidOperationException(
							string.Join(
								Environment.NewLine,
								result.Validation.Errors.Select(
									error => error.ErrorMessage)));
					}
				}
				return repository;
			});

			services.AddSingleton<IMdbxRepository>(provider =>
			{
				var initializer = provider.GetRequiredService<IDataAccessInitializer>();

				return initializer.InitializeMdbx(Path.Combine(directory, "ELPROCAD.mdbx"));
			});

			services.AddScoped<AnalysisBlockImporter>();
			services.AddScoped<LocationTreeReader>();
			services.AddScoped<CadSymbolLinkReconciler>(provider =>
			new CadSymbolLinkReconciler(provider.GetRequiredService<IMdbxRepository>()));

			return services;
		}

	}
}
