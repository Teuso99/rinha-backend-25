using rinha_api.Model;

namespace rinha_api.DTO;

public class PaymentsDTO
{
    public PaymentsByProcessorDTO Default { get; set; }
    public PaymentsByProcessorDTO Fallback { get; set; }

    public PaymentsDTO(List<Payment> defaultPayments, List<Payment> fallbackPayments)
    {
        Default = new PaymentsByProcessorDTO(defaultPayments);
        Fallback = new PaymentsByProcessorDTO(fallbackPayments);
    }
}

public class PaymentsByProcessorDTO
{
    public int TotalRequests { get; set; }
    public decimal TotalAmount { get; set; }

    public PaymentsByProcessorDTO(List<Payment> payments)
    {
        TotalRequests = payments.Count;
        TotalAmount = payments.Sum(p => p.Amount);
    }
}