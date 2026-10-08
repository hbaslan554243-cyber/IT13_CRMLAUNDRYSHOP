namespace CleanWaveCRM.Desktop;

public sealed record TenantDatabaseConfig(
    string Key,
    string Name,
    string ConnectionString,
    string SeedEmail,
    string SeedPassword,
    string SeedFullName);
