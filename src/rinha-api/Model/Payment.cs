namespace rinha_api.Model;

public class Payment
{
    public Guid CorrelationId { get; set; } 
    public decimal Amount { get; set; }
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
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