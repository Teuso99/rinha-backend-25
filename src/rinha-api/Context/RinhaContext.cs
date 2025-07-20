using Microsoft.EntityFrameworkCore;
using rinha_api.Model;

namespace rinha_api.Context;

public class RinhaContext(DbContextOptions<RinhaContext> options) : DbContext(options), IRinhaContext
{
    public DbSet<Payment> PaymentsByDefault { get; set; }
    public DbSet<Payment> PaymentsByFallback { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");
        
        optionsBuilder.UseNpgsql(connectionString);
 
        base.OnConfiguring(optionsBuilder);
    }
}

public interface IRinhaContext
{
    DbSet<Payment> PaymentsByDefault { get; set; }
    DbSet<Payment> PaymentsByFallback { get; set; }
    
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}