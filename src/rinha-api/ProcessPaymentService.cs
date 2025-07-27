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
            var paymentJson = await _queue.ListLeftPopAsync("payments");
                
            if (paymentJson.IsNullOrEmpty)
            {
                await Task.Delay(1000, stoppingToken);
                continue;
            }
            
            var payment = JsonSerializer.Deserialize<Payment>(paymentJson);
            
            try
            {
                _defaultHealthy = await IsProcessorHealthy(true);

                if (_defaultHealthy)
                {
                    var defaultResponse = await _httpClientDefault.PostAsJsonAsync("payments", payment, stoppingToken);
                    
                    if (defaultResponse.IsSuccessStatusCode)
                    {
                        payment.ProcessorName = "default";
                    
                        context.Payment.Add(payment);
                    
                        await context.SaveChangesAsync(stoppingToken);
                    
                        continue;
                    }
                }
                
                _fallbackHealthy = await IsProcessorHealthy(false);

                if (_fallbackHealthy)
                {
                    var fallbackResponse = await _httpClientFallback.PostAsJsonAsync("payments", payment, stoppingToken);
                    
                    if (fallbackResponse.IsSuccessStatusCode)
                    {
                        payment.ProcessorName = "fallback";
                    
                        context.Payment.Add(payment);
                    
                        await context.SaveChangesAsync(stoppingToken);
                    
                        continue;
                    }
                }

                await _queue.ListRightPushAsync("payments", paymentJson, flags: CommandFlags.FireAndForget);
            }
            catch (Exception e)
            {
                Console.WriteLine($"[-] {e}");
            }
        }
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

        return response is not null && !response.Failing && response.MinResponseTime <= 500;
    }
}