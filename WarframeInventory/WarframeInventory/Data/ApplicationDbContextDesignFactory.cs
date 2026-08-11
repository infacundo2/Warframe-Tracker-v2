using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using MySqlConnector;

namespace WarframeInventory.Data;

public sealed class ApplicationDbContextDesignFactory
    : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var sslModeValue = configuration["WARFRAME_TRACKER_DB_SSL_MODE"]
                           ?? configuration["ConnectionStrings:DB_SSL_MODE"]
                           ?? "Preferred";
        if (!Enum.TryParse<MySqlSslMode>(sslModeValue, true, out var sslMode))
            sslMode = MySqlSslMode.Preferred;
        _ = uint.TryParse(
            configuration["WARFRAME_TRACKER_DB_PORT"]
            ?? configuration["ConnectionStrings:DB_PORT"],
            out var port);

        var connectionString = new MySqlConnectionStringBuilder
        {
            Server = configuration["WARFRAME_TRACKER_DB_HOST"]
                     ?? configuration["ConnectionStrings:DB_HOST"]
                     ?? "localhost",
            Port = port == 0 ? 3306u : port,
            UserID = configuration["WARFRAME_TRACKER_DB_USER"]
                     ?? configuration["ConnectionStrings:DB_USER"]
                     ?? "design",
            Password = configuration["WARFRAME_TRACKER_DB_PASS"]
                       ?? configuration["ConnectionStrings:DB_PASS"]
                       ?? "design",
            Database = configuration["WARFRAME_TRACKER_DB_NAME"]
                       ?? configuration["ConnectionStrings:DB_NAME"]
                       ?? "warframe_design",
            SslMode = sslMode,
            AllowPublicKeyRetrieval = true
        }.ConnectionString;

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 0)))
            .Options;
        return new ApplicationDbContext(options);
    }
}
