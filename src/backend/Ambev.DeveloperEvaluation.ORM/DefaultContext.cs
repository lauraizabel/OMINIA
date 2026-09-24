using Ambev.DeveloperEvaluation.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System.Reflection;

namespace Ambev.DeveloperEvaluation.ORM;

public class DefaultContext : DbContext
{
    public DbSet<User> Users { get; set; }

    public DefaultContext(DbContextOptions<DefaultContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        base.OnModelCreating(modelBuilder);
    }
}
public class YourDbContextFactory : IDesignTimeDbContextFactory<DefaultContext>
{
    public DefaultContext CreateDbContext(string[] args)
    {
        var webApiDirectory = Path.GetFullPath(
            Path.Combine(Directory.GetCurrentDirectory(), "src", "backend", "Ambev.DeveloperEvaluation.WebApi"));

        if (!Directory.Exists(webApiDirectory))
        {
            webApiDirectory = Directory.GetCurrentDirectory();
        }

        IConfigurationRoot configuration = new ConfigurationBuilder()
            .SetBasePath(webApiDirectory)
            .AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var builder = new DbContextOptionsBuilder<DefaultContext>();
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        builder.UseNpgsql(
               connectionString,
               b => b.MigrationsAssembly(typeof(DefaultContext).Assembly.FullName)
        );

        return new DefaultContext(builder.Options);
    }
}
