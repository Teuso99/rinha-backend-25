using System.Data;
using Dapper;
using Npgsql;
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

    private IDbConnection CreateConnection() => new NpgsqlConnection(_connectionString);
    
    public async Task<List<Payment>> GetPaymentsByProcessorAsync(string processorName, DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
    {
        var sql = @"
            SELECT correlationid as CorrelationId, amount as Amount, requestedat as RequestedAt, processorname as ProcessorName
            FROM payment 
            WHERE processorname = @ProcessorName";

        var parameters = new DynamicParameters();
        parameters.Add("ProcessorName", processorName);

        if (from.HasValue)
        {
            sql += " AND requestedat >= @From";
            parameters.Add("From", from.Value);
        }

        if (to.HasValue)
        {
            sql += " AND requestedat <= @To";
            parameters.Add("To", to.Value);
        }

        using var connection = CreateConnection();
        var result = await connection.QueryAsync<Payment>(sql, parameters);
        return result.ToList();
    }

    public async Task<int> InsertPaymentBatchAsync(IEnumerable<Payment> payments, CancellationToken cancellationToken = default)
    {
        const string sql = @"
            INSERT INTO payment (correlationid, amount, requestedat, processorname) 
            VALUES (@CorrelationId, @Amount, @RequestedAt, @ProcessorName)";

        using var connection = CreateConnection();
        return await connection.ExecuteAsync(sql, payments);
    }
}
