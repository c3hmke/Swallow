# Swallow

A lightweight migration tool focussed around running SQL query based migrations.
Sometimes the database is just old, and it's simpler to use raw SQL than it is try and get it to play nice with a modern migration runner.

NOTE:   Currently only supports PostgresQL; because that's what the database was using. Update as needed.

## Usage
```
swallow migrate [options]           (running without options will simply run migrations)

Options (for 'migrate'):
-c, --conn <connectionString>   (or env: SWALLOW_CONNECTION_STRING)
-p, --path <migrationsPath>     (or env: SWALLOW_MIGRATIONS_PATH)

* options provided via command line will override env vars *

Examples:
swallow migrate --conn "<CONNECTION_STRING>" --path "./migrations"
SWALLOW_CONNECTION_STRING="<CONNECTION_STRING>" SWALLOW_MIGRATIONS_PATH="./migrations" swallow migrate
```

## Installation

### 1. Tool only

- Download the latest release here: https://bitgit.bit.nl/development/applications/swallow/-/releases
- Run this script to extract, copy, & allow execution:
  ```sh
   tar -xzf ~/Downloads/swallow-x.x.x.tar.gz
   sudo mv swallow-1.0.0/output/linux_x64/swallow /usr/local/bin/swallow
   sudo chmod +x /usr/local/bin/swallow
   rm -rf swallow-x.x.x
  ```
  (replace x.x.x with the version number)

### 2. Build from source

- clone & cd into the repository
- run `dotnet publish --self-contained -r linux-x64 -c release -o $PWD/output/linux_x64 -p:PublishSingleFile=true swallow.csproj`
- copy the built binary to your bin folder: `mv /output/linux_x64/swallow /usr/local/bin/swallow`

## Setup

Before using the script, the database needs to be set up for it to be able to store which migrations have run.
Use the following script (PostgreSQL, correct as necessary) to create the `__migrations` table in your database:
```postgresql
CREATE TABLE __migrations (
    filename       TEXT PRIMARY KEY,
    date_executed  TIMESTAMPTZ NOT NULL,
    checksum       TEXT NOT NULL
);
```

If the tool is being run locally then simply use the command line arguments to pass the connection string and migrations path.

## CI/CD

As part of a CI/CD flow ENV variables are the best way forward, set these on the machine running the migrations:

- SWALLOW_CONNECTION_STRING ⟵ this is used to connect to the database where the migrations will be run
- SWALLOW_MIGRATIONS_PATH   ⟵ this is the full path to the migration files

If needed, the command line arguments will override whatever is in the ENV variable.

Copy the migration files over to where this tool is running (e.g. rsync), then provide the tool with the path
for the copied files and execute the migrations.

## TODO

- get the latest migrations and run those: This should be a command. e.g. `swallow migrate`
- perform a check on the migrations which have run. e.g. `swallow verify` (use the checksums to do this)
- add in a flag to only migrate a specific file `--target`
- add in command to create migration `swallow create`. This should generate the filename in correct location, nothing more.