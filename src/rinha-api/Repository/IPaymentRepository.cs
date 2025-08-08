using rinha_api.Model;

namespace rinha_api.Repository;

public interface IPaymentRepository
{
    Task<List<Payment>> GetPaymentsByProcessorAsync(string processorName, DateTime? from, DateTime? to, CancellationToken cancellationToken = default);
    Task<int> InsertPaymentBatchAsync(IEnumerable<Payment> payments, CancellationToken cancellationToken = default);
}
