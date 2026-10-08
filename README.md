# CleanWave CRM

## Desktop setup

The desktop app reads `appsettings.json` from its output directory. The project includes `CleanWaveCRM.Desktop/appsettings.example.json` as a fallback template when no local settings file is present.

For a local development setup, copy that example to `CleanWaveCRM.Desktop/appsettings.json`, then set the central and tenant SQL Server connection strings and seed account values. The real `appsettings*.json` files are ignored by Git because they can contain passwords.

Build the solution with Visual Studio or the .NET 10 SDK using `datamodel.sln`.
