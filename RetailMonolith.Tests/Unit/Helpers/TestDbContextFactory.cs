using Microsoft.EntityFrameworkCore;
using RetailMonolith.Data;
using RetailMonolith.Models;

namespace RetailMonolith.Tests.Unit.Helpers;

public static class TestDbContextFactory
{
    public static AppDbContext CreateInMemoryContext(string databaseName = "")
    {
        if (string.IsNullOrEmpty(databaseName))
        {
            databaseName = Guid.NewGuid().ToString();
        }

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new AppDbContext(options);
    }

    public static async Task<AppDbContext> CreateContextWithSeedDataAsync()
    {
        var context = CreateInMemoryContext();

        // Add sample products
        var product1 = new Product
        {
            Id = 1,
            Sku = "TEST-001",
            Name = "Test Product 1",
            Description = "Test Description 1",
            Price = 10.00m,
            Currency = "GBP",
            IsActive = true,
            Category = "Electronics"
        };

        var product2 = new Product
        {
            Id = 2,
            Sku = "TEST-002",
            Name = "Test Product 2",
            Description = "Test Description 2",
            Price = 20.00m,
            Currency = "GBP",
            IsActive = true,
            Category = "Apparel"
        };

        var product3 = new Product
        {
            Id = 3,
            Sku = "TEST-003",
            Name = "Inactive Product",
            Description = "Inactive Test Product",
            Price = 30.00m,
            Currency = "GBP",
            IsActive = false,
            Category = "Electronics"
        };

        context.Products.AddRange(product1, product2, product3);

        // Add inventory
        context.Inventory.AddRange(
            new InventoryItem { Id = 1, Sku = "TEST-001", Quantity = 100 },
            new InventoryItem { Id = 2, Sku = "TEST-002", Quantity = 50 },
            new InventoryItem { Id = 3, Sku = "TEST-003", Quantity = 0 }
        );

        await context.SaveChangesAsync();
        return context;
    }
}
