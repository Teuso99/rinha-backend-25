using System.Text.Json;
using rinha_api.Context;
using rinha_api.Model;
using StackExchange.Redis;

namespace rinha_api;

public class ProcessPaymentService(IConnectionMultiplexer redis, IRinhaContext context) : BackgroundService
{
    private readonly IDatabase _queue = redis.GetDatabase();
    private static readonly string UrlDefault = Environment.GetEnvironmentVariable("URL_DEFAULT") ?? string.Empty;
    private static readonly string UrlFallback = Environment.GetEnvironmentVariable("URL_FALLBACK") ?? string.Empty;
    
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
                var defaultResponse = await _httpClientDefault.PostAsJsonAsync("payments", payment, stoppingToken);

                if (defaultResponse.IsSuccessStatusCode)
                {
                    payment.ProcessorName = "default";
                    
                    context.Payment.Add(payment);
                    
                    await context.SaveChangesAsync(stoppingToken);
                    
                    continue;
                }
                
                var fallbackResponse = await _httpClientFallback.PostAsJsonAsync("payments", payment, stoppingToken);
                
                if (fallbackResponse.IsSuccessStatusCode)
                {
                    payment.ProcessorName = "fallback";
                    
                    context.Payment.Add(payment);
                    
                    await context.SaveChangesAsync(stoppingToken);
                    
                    continue;
                }
                
                // If both services failed, enqueue the payment for later processing
                await _queue.ListRightPushAsync("payments", paymentJson, flags: CommandFlags.FireAndForget);
            }
            catch (Exception e)
            {
                Console.WriteLine($"[-] {e}");
            }
        }
    }
}