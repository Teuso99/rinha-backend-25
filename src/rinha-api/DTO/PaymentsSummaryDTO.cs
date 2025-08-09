using rinha_api.Model;

namespace rinha_api.DTO;

public class PaymentsSummaryDTO
{
    public PaymentsByProcessorDTO Default { get; set; }
    public PaymentsByProcessorDTO Fallback { get; set; }

    public PaymentsSummaryDTO(PaymentsByProcessorDTO defaultPayments, PaymentsByProcessorDTO fallbackPayments)
    {
        Default = defaultPayments;
        Fallback = fallbackPayments;
    }
}

public class PaymentsByProcessorDTO
{
    public int TotalRequests { get; set; }
    public decimal TotalAmount { get; set; }

    public PaymentsByProcessorDTO(int requests, decimal amount)
    {
        TotalRequests = requests;
        TotalAmount = amount;
    }
}