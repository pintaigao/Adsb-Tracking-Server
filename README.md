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
- `appsettings.Local.json` (optional, local-only, ignored by Git)
- environment variables

The most important setting is:

```bash
ConnectionStrings__Default="server=localhost;port=3306;database=adsb_tracker;user=hptg;password=your-password"
```

For local secrets and machine-specific overrides, edit your local-only file directly:

```bash
touch appsettings.Local.json
```

`appsettings.Local.json` is ignored by Git. Put your real MySQL password and machine-specific paths there.

Optional path overrides:

```bash
PiTrackSource__RawRootPath="/home/hptg/Projects/adsb-tracklog/raw"
PiTrackSource__Mode="ssh"
PiTrackSource__SshHost="192.168.86.53"
PiTrackSource__SshUser="pintaigao"
PiTrackSource__SshPort=22
PiTrackSource__RemoteRawRootPath="/home/pintaigao/Documents/ADSB-Tracker/raw"
PiTrackSource__SshIdentityFile="/Users/your-user/.ssh/id_ed25519"
PiTrackSource__SshAcceptNewHostKey=true
TrackerStorage__WorkingDirectory="data/work"
TrackerStorage__ExportDirectory="data/exports"
FEEDER_LIVE_AIRCRAFT_URL="http://192.168.86.53:8080/live-aircraft"
FEEDER_LIVE_AIRCRAFT_TOKEN=""
FLIGHT_TRAINING_SERVER_BASE_URL="http://localhost:3000"
FLIGHT_TRAINING_SERVER_SERVICE_TOKEN=""
```

Runtime toggles live in `appsettings.Local.json` as booleans:

```json
{
  "Runtime": {
    "DisableTrackScheduleWorker": false,
    "SkipDbMigrate": false
  }
}
```

`PiTrackSource` supports two modes:

- `local` (default): reads `YYYY-MM-DD.jsonl` from `RawRootPath`
- `ssh`: uses `scp` to copy one daily `jsonl` from the remote Ubuntu host into the local working directory on demand

For your current Mac + Ubuntu setup, `ssh` mode is the intended path. Make sure key-based SSH login from the Mac to the Ubuntu host works non-interactively before running schedules.

## EF Core migrations

Install the local EF tool if needed:

```bash
DOTNET_CLI_HOME=/tmp dotnet tool restore
```

Create a migration:

```bash
DOTNET_CLI_HOME=/tmp dotnet dotnet-ef migrations add InitialTrackSchedules
```

Apply migrations:

```bash
DOTNET_CLI_HOME=/tmp dotnet dotnet-ef database update
```

The app also runs `Database.Migrate()` on startup, so once the first migration exists it will apply pending migrations automatically.

## Run locally

```bash
DOTNET_CLI_HOME=/tmp dotnet run --launch-profile http
```

To run only the live-aircraft path without background MySQL polling:

```bash
DOTNET_CLI_HOME=/tmp dotnet run --launch-profile http
```

Set the local runtime toggles first if you want that behavior:

```json
{
  "Runtime": {
    "DisableTrackScheduleWorker": true,
    "SkipDbMigrate": true
  }
}
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
