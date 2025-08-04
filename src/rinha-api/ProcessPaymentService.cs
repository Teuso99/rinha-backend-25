using System.Text.Json;
using rinha_api.Context;
using rinha_api.DTO;
using rinha_api.Model;
using StackExchange.Redis;

namespace rinha_api;

public class ProcessPaymentService(IConnectionMultiplexer redis, IRinhaContext context) : BackgroundService
{
    private readonly IDatabase _queue = redis.GetDatabase();
    private static readonly string UrlDefault = Environment.GetEnvironmentVariable("URL_DEFAULT") ?? string.Empty;
    private static readonly string UrlFallback = Environment.GetEnvironmentVariable("URL_FALLBACK") ?? string.Empty;
    private DateTime _defaultHealthCheckLastCall = DateTime.MinValue;
    private DateTime _fallbackHealthCheckLastCall = DateTime.MinValue;
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
            var paymentJson = await _queue.ListLeftPopAsync("payments", 100);
                
            if (paymentJson is null || paymentJson.Length <= 0)
            {
                await Task.Delay(1000, stoppingToken);
                continue;
            }

            List<Payment> payments = paymentJson.Select(p => JsonSerializer.Deserialize<Payment>(p)).ToList();
            
            try
            {
                _defaultHealthy = await IsProcessorHealthy(true);

                if (_defaultHealthy)
                {
                    await ProcessByDefault(payments, stoppingToken);
                    continue;
                }

                _fallbackHealthy = await IsProcessorHealthy(false);

                if (_fallbackHealthy)
                {
                    await ProcessByFallback(payments, stoppingToken);
                    continue;
                }

                await _queue.ListRightPushAsync("payments", paymentJson, flags: CommandFlags.FireAndForget);
            }
            catch (Exception e)
            {
                Console.WriteLine($"[-] {e}");
            }
        }
    }

    private async Task ProcessByDefault(List<Payment> payments, CancellationToken stoppingToken)
    {
        foreach (var payment in payments)
        {
            var response = await _httpClientDefault.PostAsJsonAsync("payments", payment, stoppingToken);
            
            if (!response.IsSuccessStatusCode)
            {
                await _queue.ListRightPushAsync("payments", JsonSerializer.Serialize(payment), flags: CommandFlags.FireAndForget);
                continue;
            }
            
            payment.ProcessorName = "default";
            context.Payment.Add(payment);
        }
  
        await context.SaveChangesAsync(stoppingToken);
    }
    
    private async Task ProcessByFallback(List<Payment> payments, CancellationToken stoppingToken)
    {
        foreach (var payment in payments)
        {
            var response = await _httpClientFallback.PostAsJsonAsync("payments", payment, stoppingToken);

            if (!response.IsSuccessStatusCode)
            {
                await _queue.ListRightPushAsync("payments", JsonSerializer.Serialize(payment), flags: CommandFlags.FireAndForget);
                continue;
            }

            payment.ProcessorName = "fallback";
            context.Payment.Add(payment);
        }
  
        await context.SaveChangesAsync(stoppingToken);
    }
    
    private async Task<bool> IsProcessorHealthy(bool isDefault)
    {
        if (isDefault && (DateTime.UtcNow - _defaultHealthCheckLastCall).TotalSeconds < 5)
        {
            return _defaultHealthy;
        }
        
        if (!isDefault && (DateTime.UtcNow - _fallbackHealthCheckLastCall).TotalSeconds < 5)
        {
            return _fallbackHealthy;
        }
        
        var healthcheck = isDefault ? await _httpClientDefault.GetAsync("payments/service-health") 
                                                        : await _httpClientFallback.GetAsync("payments/service-health");
        
        if (!healthcheck.IsSuccessStatusCode)
            return false;
        
        if (isDefault)
        {
            _defaultHealthCheckLastCall = DateTime.UtcNow;
        }
        else
        {
            _fallbackHealthCheckLastCall = DateTime.UtcNow;
        }
        
        var response = JsonSerializer.Deserialize<HealthcheckDTO>(await healthcheck.Content.ReadAsStringAsync());

        return response is not null && !response.Failing && response.MinResponseTime <= 100;
    }
}