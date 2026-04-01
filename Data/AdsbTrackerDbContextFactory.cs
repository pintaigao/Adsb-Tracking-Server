using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace ADSB.Tracker.Server.Data;

public sealed class AdsbTrackerDbContextFactory : IDesignTimeDbContextFactory<AdsbTrackerDbContext> {
	public AdsbTrackerDbContext CreateDbContext(string[] args) {
		var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

		var configuration = new ConfigurationBuilder()
			.SetBasePath(Directory.GetCurrentDirectory())
			.AddJsonFile("appsettings.json", optional: false)
			.AddJsonFile($"appsettings.{environment}.json", optional: true)
			.AddJsonFile("appsettings.Local.json", optional: true)
			.AddEnvironmentVariables()
			.Build();

		var connectionString = configuration.GetConnectionString("Default") ?? throw new InvalidOperationException("Connection string 'Default' is required.");

		var optionsBuilder = new DbContextOptionsBuilder<AdsbTrackerDbContext>();
		optionsBuilder.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 36)));
		return new AdsbTrackerDbContext(optionsBuilder.Options);
	}
}