using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RetailMonolith.Data;

namespace RetailMonolith.Tests.Integration.Helpers;

public class IntegrationTestFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove all DbContext related services
            var dbContextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (dbContextDescriptor != null)
            {
                services.Remove(dbContextDescriptor);
            }

            var dbContextOptionsDescriptor = services.Where(
                d => d.ServiceType.IsGenericType &&
                     d.ServiceType.GetGenericTypeDefinition() == typeof(DbContextOptions<>))
                .ToList();
            
            foreach (var descriptor in dbContextOptionsDescriptor)
            {
                services.Remove(descriptor);
            }

            // Add in-memory database for testing with unique name per test run
            var dbName = $"InMemoryTestDb_{Guid.NewGuid()}";
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseInMemoryDatabase(dbName);
            });
        });
        
        builder.UseEnvironment("Testing");
    }
    
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Clean up in-memory database
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureDeleted();
        }
        base.Dispose(disposing);
    }
}
