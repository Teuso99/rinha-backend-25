using System.ComponentModel.DataAnnotations;

namespace rinha_api.Model;

public class Payment
{
    [Key]
    public Guid CorrelationId { get; set; } 
    public decimal Amount { get; set; }
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    public Payment()
    {
        
    }
    
    public Payment(Guid correlationId, decimal amount)
    {
        CorrelationId = correlationId;
        Amount = amount;
    }
}