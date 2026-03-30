# ADSB Tracker Server

`ADSB-Tracker-Server` is the dedicated ADS-B track scheduling microservice for `Flight-Training`.

## What it does

- stores user-scoped track schedule jobs in MySQL
- polls for due UTC/Zulu schedules
- reads Raspberry Pi raw `jsonl` logs
- filters matching track points
- exports KML files

## Local prerequisites

- .NET SDK 8+
- MySQL running locally
- a database created for this service, for example:

```sql
CREATE DATABASE adsb_tracker;
```

## Local configuration

The service reads configuration from:

- `appsettings.json`
- `appsettings.Development.json`
- environment variables

The most important setting is:

```bash
ConnectionStrings__Default="server=localhost;port=3306;database=adsb_tracker;user=hptg;password=your-password"
```

Optional path overrides:

```bash
PiTrackSource__RawRootPath="/home/hptg/Projects/adsb-tracklog/raw"
TrackerStorage__WorkingDirectory="data/work"
TrackerStorage__ExportDirectory="data/exports"
FEEDER_LIVE_AIRCRAFT_URL="http://192.168.86.53:8080/live-aircraft"
FEEDER_LIVE_AIRCRAFT_TOKEN=""
DISABLE_TRACK_SCHEDULE_WORKER=1
```

## EF Core migrations

Install the local EF tool if needed:

```bash
DOTNET_CLI_HOME=/tmp dotnet tool restore
```

Create a migration:

```bash
SKIP_DB_MIGRATE=1 DOTNET_CLI_HOME=/tmp dotnet dotnet-ef migrations add InitialTrackSchedules
```

Apply migrations:

```bash
SKIP_DB_MIGRATE=1 DOTNET_CLI_HOME=/tmp dotnet dotnet-ef database update
```

The app also runs `Database.Migrate()` on startup, so once the first migration exists it will apply pending migrations automatically.

## Run locally

```bash
DOTNET_CLI_HOME=/tmp dotnet run --launch-profile http
```

To run only the live-aircraft path without background MySQL polling:

```bash
SKIP_DB_MIGRATE=1 DISABLE_TRACK_SCHEDULE_WORKER=1 DOTNET_CLI_HOME=/tmp dotnet run --launch-profile http
```

The default local HTTP URL is:

```text
http://localhost:5053
```

## Internal API

- `GET /adsb/flights/live-aircraft`
- `POST /adsb/flights/track-schedules`
- `GET /adsb/flights/track-schedules`
- `GET /adsb/flights/track-schedules/{id}`
- `POST /adsb/flights/track-schedules/{id}/cancel`
- `GET /adsb/flights/track-schedules/{id}/executions`
- `GET /adsb/flights/track-schedules/executions/{executionId}/download`
