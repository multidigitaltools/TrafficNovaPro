namespace TrafficNova.Data;

/// <summary>Creates short-lived AppDbContext instances per operation.</summary>
public class AppDbContextFactory
{
    private readonly string _dbPath;

    public AppDbContextFactory(string dbPath)
    {
        _dbPath = dbPath;
    }

    public AppDbContext Create() => new(_dbPath);
}
