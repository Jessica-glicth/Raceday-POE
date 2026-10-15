using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Data;

namespace RaceDay.Api.Tests;

/// <summary>
/// Boots the real API in memory, but swaps SQL Server for an isolated in-memory database
/// so the tests run anywhere (including GitHub Actions) with no setup.
/// </summary>
public class RaceDayFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"RaceDayTests-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["SeedData"] = "false" }));

        builder.ConfigureServices(services =>
            services.AddDbContext<RaceDayDbContext>(o => o.UseInMemoryDatabase(_dbName)));
    }
}
