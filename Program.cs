using ADSB.Tracker.Server.Data;
using ADSB.Tracker.Server.Options;
using ADSB.Tracker.Server.Services;
using ADSB.Tracker.Server.Workers;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<PiTrackSourceOptions>(
    builder.Configuration.GetSection(PiTrackSourceOptions.SectionName));
builder.Services.Configure<TrackerStorageOptions>(
    builder.Configuration.GetSection(TrackerStorageOptions.SectionName));
builder.Services.Configure<FeederLiveAircraftOptions>(
    builder.Configuration.GetSection(FeederLiveAircraftOptions.SectionName));

var connectionString =
    builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Connection string 'Default' is required.");
var mysqlVersion = new MySqlServerVersion(new Version(8, 0, 36));

builder.Services.AddDbContext<AdsbTrackerDbContext>(options =>
    options.UseMySql(connectionString, mysqlVersion));

builder.Services.AddHttpContextAccessor();
builder.Services.AddControllers();

builder.Services.AddScoped<TrackScheduleService>();
builder.Services.AddScoped<PiTrackSourceService>();
builder.Services.AddScoped<TrackExportService>();
builder.Services.AddHttpClient<FeederLiveAircraftService>();
builder.Services.AddHostedService<TrackScheduleExecutionWorker>();

var app = builder.Build();

var skipDbMigrate =
    string.Equals(
        builder.Configuration["SKIP_DB_MIGRATE"],
        "1",
        StringComparison.OrdinalIgnoreCase)
    || string.Equals(
        Environment.GetEnvironmentVariable("SKIP_DB_MIGRATE"),
        "1",
        StringComparison.OrdinalIgnoreCase);

if (!skipDbMigrate)
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AdsbTrackerDbContext>();
    dbContext.Database.Migrate();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
