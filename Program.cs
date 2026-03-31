using ADSB.Tracker.Server.Data;
using ADSB.Tracker.Server.Options;
using ADSB.Tracker.Server.Services;
using ADSB.Tracker.Server.Workers;
using Microsoft.EntityFrameworkCore;

/*
 * 这个服务的职责刻意收得很窄：
 * - 它自己拥有 ADS-B 轨迹导出的 schedule / execution 数据库
 * - 它对外暴露一个很薄的 live-aircraft 实时接口，底层数据来自 Ubuntu feeder
 * - 它在 schedule 完成后把导出结果回调给 Flight-Training
 */
var builder = WebApplication.CreateBuilder(args);

/*
 * 在共享的 appsettings 之后再加载本机覆盖配置，
 * 这样密钥和机器相关路径就可以不进 Git，同时仍然覆盖默认值。
 */
builder.Configuration
	.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true)
	.AddEnvironmentVariables();

// 把配置绑定到 Options 对象上，方便后续注入使用。
builder.Services.Configure<PiTrackSourceOptions>(builder.Configuration.GetSection(PiTrackSourceOptions.SectionName));
builder.Services.Configure<TrackerStorageOptions>(builder.Configuration.GetSection(TrackerStorageOptions.SectionName));
builder.Services.Configure<FeederLiveAircraftOptions>(builder.Configuration.GetSection(FeederLiveAircraftOptions.SectionName));
builder.Services.Configure<FlightTrainingServerOptions>(builder.Configuration.GetSection(FlightTrainingServerOptions.SectionName));

var connectionString = builder.Configuration.GetConnectionString("Default") ?? throw new InvalidOperationException("Connection string 'Default' is required.");
var mysqlVersion = new MySqlServerVersion(new Version(8, 0, 36));

/*
 * ADSB-Tracker-Server 自己拥有 `adsb_tracker` 这套 schema。
 * 其他服务应该调用它的 API，而不是直接读写这套数据库。
 */
builder.Services.AddDbContext<AdsbTrackerDbContext>(options => options.UseMySql(connectionString, mysqlVersion));

builder.Services.AddHttpContextAccessor();
builder.Services.AddControllers();

/*
 * 核心服务：
 * - TrackScheduleService 负责串起 schedule 生命周期
 * - PiTrackSourceService 负责拿到原始 jsonl
 * - TrackExportService 负责过滤点并写出 KML
 */
builder.Services.AddScoped<TrackScheduleService>();
builder.Services.AddScoped<PiTrackSourceService>();
builder.Services.AddScoped<TrackExportService>();
builder.Services.AddHttpClient<FeederLiveAircraftService>();
builder.Services.AddHttpClient<FlightImportService>();

var disableTrackScheduleWorker = builder.Configuration.GetSection("Runtime").GetValue<bool>("DisableTrackScheduleWorker");
/*
 * Worker 是内部的“时钟”：
 * 它负责轮询到期的 schedule。
 * 如果本地只想调试 live-aircraft，这个 worker 可以关闭。
 */
if (!disableTrackScheduleWorker) {
	builder.Services.AddHostedService<TrackScheduleExecutionWorker>();
}

var app = builder.Build();
var skipDbMigrate = builder.Configuration.GetSection("Runtime").GetValue<bool>("SkipDbMigrate");

/*
 * 正常启动时，先把数据库 schema 迁移到最新，再开始对外提供接口。
 */
if (!skipDbMigrate) {
	using var scope = app.Services.CreateScope();
	var dbContext = scope.ServiceProvider.GetRequiredService<AdsbTrackerDbContext>();
	dbContext.Database.Migrate();
}

/*
 * 这是一个内部微服务，所以 HTTP pipeline 故意保持得很薄。
 */
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapGroup("/api/v1").MapControllers();

app.Run();
