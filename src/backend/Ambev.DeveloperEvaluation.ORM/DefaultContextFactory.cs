using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Ambev.DeveloperEvaluation.ORM;

public sealed class DefaultContextFactory : IDesignTimeDbContextFactory<DefaultContext>
{
    private const string WebApiProjectName = "Ambev.DeveloperEvaluation.WebApi";

    public DefaultContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            var webApiDirectory = FindWebApiDirectory(Directory.GetCurrentDirectory());
            var configuration = new ConfigurationBuilder()
                .SetBasePath(webApiDirectory)
                .AddJsonFile("appsettings.json")
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var options = new DbContextOptionsBuilder<DefaultContext>()
            .UseNpgsql(
                connectionString,
                provider => provider.MigrationsAssembly(typeof(DefaultContext).Assembly.FullName))
            .Options;

        return new DefaultContext(options);
    }

    private static string FindWebApiDirectory(string startDirectory)
    {
        for (var current = new DirectoryInfo(startDirectory); current is not null; current = current.Parent)
        {
            var directCandidate = Path.Combine(current.FullName, WebApiProjectName);
            if (File.Exists(Path.Combine(directCandidate, "appsettings.json")))
                return directCandidate;

            var repositoryCandidate = Path.Combine(
                current.FullName,
                "src",
                "backend",
                WebApiProjectName);
            if (File.Exists(Path.Combine(repositoryCandidate, "appsettings.json")))
                return repositoryCandidate;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate the {WebApiProjectName} directory from '{startDirectory}'.");
    }
}
