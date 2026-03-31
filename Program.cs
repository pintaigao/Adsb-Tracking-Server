using ADSB.Tracker.Server.Data;
using ADSB.Tracker.Server.Options;
using ADSB.Tracker.Server.Services;
using ADSB.Tracker.Server.Workers;
using Microsoft.EntityFrameworkCore;

// This service stays intentionally narrow:
// - it owns the schedule/execution database for ADS-B track exports
// - it exposes a thin real-time live-aircraft endpoint backed by the Ubuntu feeder
// - it publishes completed schedule exports back to Flight-Training
var builder = WebApplication.CreateBuilder(args);

// Load machine-local overrides after shared appsettings so secrets and per-machine paths
// can stay out of Git while still overriding defaults.
builder.Configuration
	.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true)
	.AddEnvironmentVariables();

builder.Services.Configure<PiTrackSourceOptions>(builder.Configuration.GetSection(PiTrackSourceOptions.SectionName));
builder.Services.Configure<TrackerStorageOptions>(builder.Configuration.GetSection(TrackerStorageOptions.SectionName));
builder.Services.Configure<FeederLiveAircraftOptions>(builder.Configuration.GetSection(FeederLiveAircraftOptions.SectionName));
builder.Services.Configure<FlightTrainingServerOptions>(builder.Configuration.GetSection(FlightTrainingServerOptions.SectionName));

var connectionString = builder.Configuration.GetConnectionString("Default") ?? throw new InvalidOperationException("Connection string 'Default' is required.");
var mysqlVersion = new MySqlServerVersion(new Version(8, 0, 36));

// ADSB-Tracker-Server owns its own schema (`adsb_tracker`). Other services should call its API
// instead of reaching into this database directly.
builder.Services.AddDbContext<AdsbTrackerDbContext>(options => options.UseMySql(connectionString, mysqlVersion));

builder.Services.AddHttpContextAccessor();
builder.Services.AddControllers();

// Core services:
// - TrackScheduleService orchestrates schedule lifecycle
// - PiTrackSourceService fetches the raw jsonl file
// - TrackExportService filters points and writes KML
builder.Services.AddScoped<TrackScheduleService>();
builder.Services.AddScoped<PiTrackSourceService>();
builder.Services.AddScoped<TrackExportService>();
builder.Services.AddHttpClient<FeederLiveAircraftService>();
builder.Services.AddHttpClient<FlightImportService>();

// The worker is the internal "clock" for due schedules. It can be disabled when only the
// live-aircraft path is needed during local debugging.
if (!IsFlagEnabled(builder.Configuration, "DISABLE_TRACK_SCHEDULE_WORKER")) {
	builder.Services.AddHostedService<TrackScheduleExecutionWorker>();
}

var app = builder.Build();

var skipDbMigrate = IsFlagEnabled(builder.Configuration, "SKIP_DB_MIGRATE");

// On normal startup, make sure the schema is current before requests start hitting controllers.
if (!skipDbMigrate) {
	using var scope = app.Services.CreateScope();
	var dbContext = scope.ServiceProvider.GetRequiredService<AdsbTrackerDbContext>();
	dbContext.Database.Migrate();
}

// This is an internal service, so the HTTP pipeline stays deliberately small.
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();

static bool IsFlagEnabled(IConfiguration configuration, string key) => string.Equals(configuration[key], "1", StringComparison.OrdinalIgnoreCase) || string.Equals(Environment.GetEnvironmentVariable(key), "1", StringComparison.OrdinalIgnoreCase);
