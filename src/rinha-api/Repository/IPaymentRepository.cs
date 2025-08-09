using rinha_api.DTO;
using rinha_api.Model;

namespace rinha_api.Repository;

public interface IPaymentRepository
{
    Task<PaymentsSummaryDTO> GetPaymentsSummaryAsync(DateTime? from, DateTime? to, CancellationToken cancellationToken = default);
    Task<int> InsertPaymentBatchAsync(IEnumerable<Payment> payments, CancellationToken cancellationToken = default);
    Task InsertPaymentAsync(Payment payment, CancellationToken cancellationToken = default);
}
