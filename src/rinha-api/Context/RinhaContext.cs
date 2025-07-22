using Microsoft.EntityFrameworkCore;
using rinha_api.Model;

namespace rinha_api.Context;

public class RinhaContext(DbContextOptions<RinhaContext> options) : DbContext(options), IRinhaContext
{
    public DbSet<Payment> Payment { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");
        
        optionsBuilder.UseNpgsql(connectionString);
 
        base.OnConfiguring(optionsBuilder);
    }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Payment>().ToTable("payment");
        
        base.OnModelCreating(modelBuilder);
    }
}

public interface IRinhaContext
{
    DbSet<Payment> Payment { get; set; }
    
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}