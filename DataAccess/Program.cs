using ELPROCAD.API.DI;
using ELPROCAD.API.Provider;
using System.Data.Common;
using ELPROCAD.API.Services;

var builder = WebApplication.CreateBuilder(args);

const string projectDirectory = @"C:\Dev\POC\81346\TestProject";
Directory.CreateDirectory(projectDirectory);
var dbPath = Path.Combine(projectDirectory, "ELPROCAD.pdsx");
var connectionStringBuilder = new DbConnectionStringBuilder
{
	["XpoProvider"] = "VistaDB6",
	["Data Source"] = dbPath
};

builder.Services.AddElprocadDataAccess(
	connectionStringBuilder.ConnectionString);

builder.Services.AddControllers();
//builder.Services.AddScoped<IProjectReader, ProjectReader>();
builder.Services.AddScoped<IEngineeringObjectReader, EngineeringObjectReader>();
builder.Services.AddScoped<IDocumentReader, DocumentReader>();
builder.Services.AddScoped<ISheetReader, SheetReader>();
builder.Services.AddScoped<ILocationReader, LocationReader>();
builder.Services.AddScoped<IFunctionReader, FunctionReader>();

var app = builder.Build();

using(var scope=app.Services.CreateScope())
{
	var initalizer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();

	initalizer.Initialize();
}

app.MapGet("/health", () => Results.Ok(new
{
	status = "ok",
	service = "ELPROCAD.API"
}));

if (app.Environment.IsDevelopment())
{
	using (var scope = app.Services.CreateScope())
	{
		var testData = scope.ServiceProvider
			.GetRequiredService<DatabaseTestData>();

		var projectId = testData.Create(projectDirectory);

		app.Logger.LogInformation(
			"Testprojekt tillgängligt: {ProjectId}",
			projectId);
	}

	using (var scope = app.Services.CreateScope())
	{
		var verifier = scope.ServiceProvider
			.GetRequiredService<DatabaseTestVerifier>();

		verifier.Verify("TEST-81346");

		app.Logger.LogInformation(
			"Databasverifiering klar: GUID, objekttillhörighet, " +
			"rekursiva strukturer och representerande objekt fungerar.");
	}
}

app.MapControllers();
app.Run();
