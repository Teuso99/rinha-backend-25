using System.Data;
using Dapper;
using Npgsql;
using rinha_api.DTO;
using rinha_api.Model;

namespace rinha_api.Repository;

public class PaymentRepository : IPaymentRepository
{
    private readonly string _connectionString;

    public PaymentRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") 
                           ?? Environment.GetEnvironmentVariable("DB_CONNECTION_STRING")
                           ?? throw new InvalidOperationException("Connection string not found");
    }
    
    public async Task<PaymentsSummaryDTO> GetPaymentsSummaryAsync(DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
    {
        var sql =  """
                   SELECT
                       COUNT(*) FILTER (WHERE upper(processorname) = 'DEFAULT') AS DefaultTotalRequests,
                       COALESCE(SUM(amount) FILTER (WHERE upper(processorname) = 'DEFAULT'), 0) AS DefaultTotalAmount,
                       COUNT(*) FILTER (WHERE upper(processorname) = 'FALLBACK') AS FallbackTotalRequests,
                       COALESCE(SUM(amount) FILTER (WHERE upper(processorname) = 'FALLBACK'), 0) AS FallbackTotalAmount
                   FROM payment
                   WHERE requestedat BETWEEN @from AND @to;
                   """;

        var startDate = from ?? DateTime.MinValue;
        var endDate = to ?? DateTime.MaxValue;
        
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("from", NpgsqlTypes.NpgsqlDbType.TimestampTz, startDate);
        cmd.Parameters.AddWithValue("to", NpgsqlTypes.NpgsqlDbType.TimestampTz, endDate);
        
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);

        return new PaymentsSummaryDTO(
            new PaymentsByProcessorDTO(reader.GetInt32(0), reader.GetDecimal(1)),
            new PaymentsByProcessorDTO(reader.GetInt32(2), reader.GetDecimal(3))
        );
    }

    public async Task InsertPaymentAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        const string sql = """
                           INSERT INTO payment (correlationid, amount, requestedat, processorname) 
                           VALUES (@CorrelationId, @Amount, @RequestedAt, @ProcessorName)
                           """;
        
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.Add("CorrelationId", NpgsqlTypes.NpgsqlDbType.Uuid).Value = payment.CorrelationId;
        cmd.Parameters.Add("Amount", NpgsqlTypes.NpgsqlDbType.Numeric).Value = payment.Amount;
        cmd.Parameters.Add("RequestedAt", NpgsqlTypes.NpgsqlDbType.TimestampTz).Value = payment.RequestedAt;
        cmd.Parameters.Add("ProcessorName", NpgsqlTypes.NpgsqlDbType.Varchar).Value = payment.ProcessorName;
        
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
    
    public async Task<int> InsertPaymentBatchAsync(IEnumerable<Payment> payments, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
