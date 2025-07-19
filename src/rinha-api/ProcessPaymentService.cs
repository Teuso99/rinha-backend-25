using System.Text.Json;
using StackExchange.Redis;

namespace rinha_api;

public class ProcessPaymentService(IConnectionMultiplexer redis) : BackgroundService
{
    private readonly IDatabase _queue = redis.GetDatabase();
    private readonly string _urlDefault = Environment.GetEnvironmentVariable("URL_DEFAULT") ?? string.Empty;
    private readonly string _urlFallback = Environment.GetEnvironmentVariable("URL_FALLBACK") ?? string.Empty;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        //sleep for 5sec
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        Console.WriteLine("é o mengudo pora");

        //dequeue payment JSON into Payment object setting RequestedAt to DateTime.UtcNow

        // try sending the payment to the default service

        //if it fails, try sending it to the fallback service

        // if both fail, try one more time with the default service

        // if it fails again, queue the payment for later processing

        //when the payment is successfully processed, save it to the database
    }
}