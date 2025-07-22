using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace rinha_api.Model;

[Table("payment")]
public class Payment
{
    [Key]
    [Column("correlationid")]
    public Guid CorrelationId { get; set; } 
    
    [Column("amount")]
    public decimal Amount { get; set; }
    
    [Column("requestedat")]
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    
    [Column("processorname")]
    public string ProcessorName { get; set; }

    public Payment()
    {
        
    }
    
    public Payment(Guid correlationId, decimal amount, string processorName)
    {
        CorrelationId = correlationId;
        Amount = amount;
        ProcessorName = processorName;
    }
}