using System.Text.Json;
using rinha_api.DTO;
using rinha_api.Model;
using rinha_api.Repository;
using StackExchange.Redis;

namespace rinha_api;

public class ProcessPaymentService(IConnectionMultiplexer redis, IPaymentRepository paymentRepository) : BackgroundService
{
    private readonly IDatabase _queue = redis.GetDatabase();
    private static readonly string UrlDefault = Environment.GetEnvironmentVariable("URL_DEFAULT") ?? string.Empty;
    private static readonly string UrlFallback = Environment.GetEnvironmentVariable("URL_FALLBACK") ?? string.Empty;
    private DateTime _healthCheckLastCall = DateTime.MinValue;
    private bool _defaultHealthy = true;
    private bool _fallbackHealthy = true;
    
    private readonly HttpClient _httpClientDefault = new()
    {
        BaseAddress = new Uri(UrlDefault)
    };
    
    private readonly HttpClient _httpClientFallback = new()
    {
        BaseAddress = new Uri(UrlFallback)
    };
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Console.WriteLine("[-] Running...");

        while (!stoppingToken.IsCancellationRequested)
        {
            var paymentJson = await _queue.ListLeftPopAsync("payments");
                
            if (paymentJson.HasValue)
            {
                await Task.Delay(5, stoppingToken);
                continue;
            }
            
            if (paymentJson.IsNullOrEmpty) continue;
                
            var payment = JsonSerializer.Deserialize<Payment>(paymentJson, RinhaSerializerContext.Default.Payment);

            try
            {
                await IsProcessorHealthy().ConfigureAwait(false);
                
                var processedPayments = new List<Payment>();

                if (_defaultHealthy)
                {
                    var processed = await ProcessByDefault(payment, stoppingToken).ConfigureAwait(false);
                    if (processed != null)
                    {
                        processedPayments.Add(processed);
                    }
                }
                else if (_fallbackHealthy)
                {
                    var processed = await ProcessByFallback(payment, stoppingToken).ConfigureAwait(false);
                    if (processed != null)
                    {
                        processedPayments.Add(processed);
                    }
                }
                else
                {
                    await _queue.ListRightPushAsync("payments", 
                        JsonSerializer.Serialize(payment, RinhaSerializerContext.Default.Payment), 
                        flags: CommandFlags.FireAndForget).ConfigureAwait(false);
                }
                
                if (processedPayments.Count > 5)
                {
                    await paymentRepository.InsertPaymentBatchAsync(processedPayments, stoppingToken).ConfigureAwait(false);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"[-] {e}");
            }
        }
    }

    private async Task<Payment?> ProcessByDefault(Payment payment, CancellationToken stoppingToken)
    {
        var response = await _httpClientDefault
                                .PostAsJsonAsync("payments", payment, stoppingToken)
                                .ConfigureAwait(false);
            
        if (!response.IsSuccessStatusCode)
        {
            await _queue.ListRightPushAsync("payments", 
                                            JsonSerializer.Serialize(payment, RinhaSerializerContext.Default.Payment), 
                                            flags: CommandFlags.FireAndForget).ConfigureAwait(false);
            return null;
        }
            
        payment.ProcessorName = "default";
        return payment;
    }
    
    private async Task<Payment?> ProcessByFallback(Payment payment, CancellationToken stoppingToken)
    {
        var response = await _httpClientFallback
                                .PostAsJsonAsync("payments", payment, stoppingToken)
                                .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            await _queue.ListRightPushAsync("payments", 
                                            JsonSerializer.Serialize(payment, RinhaSerializerContext.Default.Payment), 
                                            flags: CommandFlags.FireAndForget).ConfigureAwait(false);
            return null;
        }
            
        payment.ProcessorName = "fallback";
        return payment;
    }
    
    private async Task IsProcessorHealthy()
    {
        if (DateTime.UtcNow.Subtract(_healthCheckLastCall).TotalSeconds <= 5)
            return;

        try
        {
            var taskDefault = _httpClientDefault.GetAsync("payments/service-health");
            var taskFallback = _httpClientFallback.GetAsync("payments/service-health");

            await Task.WhenAll(taskDefault, taskFallback).ConfigureAwait(false);
            
            _healthCheckLastCall = DateTime.UtcNow;

            if (!taskDefault.Result.IsSuccessStatusCode ||
                !taskFallback.Result.IsSuccessStatusCode)
            {
                return;
            }
            
            var healthcheckDefault = await taskDefault.Result.Content.ReadFromJsonAsync<HealthcheckDTO>
                                                    (RinhaSerializerContext.Default.HealthcheckDTO).ConfigureAwait(false);
            
            var healthcheckFallback = await taskFallback.Result.Content.ReadFromJsonAsync<HealthcheckDTO>
                                                    (RinhaSerializerContext.Default.HealthcheckDTO).ConfigureAwait(false);

            if (healthcheckDefault is null || healthcheckFallback is null)
            {
                return;
            }
            
            _defaultHealthy = healthcheckDefault is { Failing: false, MinResponseTime: < 100 };
            _fallbackHealthy = healthcheckFallback is { Failing: false, MinResponseTime: < 100 };
        }
        catch (Exception e)
        {
            Console.WriteLine($"[-] Health check failed: {e}");
        }
    }
}