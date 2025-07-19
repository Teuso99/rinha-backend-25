using Microsoft.EntityFrameworkCore;
using rinha_api.Model;

namespace rinha_api.Context;

public class RinhaContext : DbContext
{
    public DbSet<Payment> Payments { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");
        
        optionsBuilder.UseNpgsql(connectionString);
 
        base.OnConfiguring(optionsBuilder);
    }
}