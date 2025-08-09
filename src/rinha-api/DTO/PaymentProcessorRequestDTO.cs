namespace rinha_api.DTO;

public class PaymentProcessorRequestDTO(Guid correlationId, decimal amount)
{
    public Guid CorrelationId { get; set; } = correlationId;
    public decimal Amount { get; set; } = amount;
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
}