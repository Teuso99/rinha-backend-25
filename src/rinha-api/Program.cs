using System.Runtime;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using rinha_api;
using rinha_api.Context;
using rinha_api.DTO;
using rinha_api.Model;
using StackExchange.Redis;

var builder = WebApplication.CreateSlimBuilder(args);

var urlRedis = Environment.GetEnvironmentVariable("URL_REDIS") ?? "localhost";
urlRedis += ",abortConnect=false";

builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(urlRedis));

builder.Services.AddDbContext<IRinhaContext, RinhaContext>(options =>
{
    var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");
    
    if (string.IsNullOrEmpty(connectionString))
    {
        throw new InvalidOperationException("DB_CONNECTION_STRING environment variable is not set.");
    }
    
    options.UseNpgsql(connectionString);
});

builder.Services.AddHostedService<ProcessPaymentService>();

builder.Services.ConfigureHttpJsonOptions(options => {
    options.SerializerOptions.TypeInfoResolver = RinhaSerializerContext.Default;
    options.SerializerOptions.DefaultBufferSize = 256;
});

builder.Services.Configure<JsonSerializerOptions>(options => {
    options.TypeInfoResolver = RinhaSerializerContext.Default;
    options.DefaultBufferSize = 256;
});

builder.Logging.ClearProviders();
GCSettings.LatencyMode = GCLatencyMode.LowLatency;

var app = builder.Build();

app.UseRouting();

app.MapGet("/payments-summary", async (DateTime? from, DateTime? to) =>
{
    var paymentsByDefault = await app.Services.GetRequiredService<RinhaContext>().Payment
                                                .AsNoTracking().Where(p =>
                                                    p.ProcessorName == "default" &&
                                                    (from == null || p.RequestedAt >= from.Value) &&
                                                    (to == null || p.RequestedAt <= to.Value)).ToListAsync();
    
    var paymentsByFallback = await app.Services.GetRequiredService<RinhaContext>().Payment
                                                .AsNoTracking().Where(p =>
                                                    p.ProcessorName == "fallback" &&
                                                    (from == null || p.RequestedAt >= from.Value) &&
                                                    (to == null || p.RequestedAt <= to.Value)).ToListAsync();
    
    return Results.Ok(new PaymentsDTO(paymentsByDefault, paymentsByFallback));
});

app.MapPost("/payments", async (PaymentRequestDTO request, [FromServices] IConnectionMultiplexer redis) =>
{
    var queue = redis.GetDatabase();
    
    var payment = new Payment(request.CorrelationId, request.Amount, string.Empty);
    
    var result = await queue.ListRightPushAsync("payments", JsonSerializer.Serialize(payment));

    return result > 0 ? Results.Created() : Results.BadRequest();
});

app.Run();

record PaymentRequestDTO(Guid CorrelationId, decimal Amount);

[JsonSerializable(typeof(PaymentRequestDTO))]
[JsonSerializable(typeof(PaymentsDTO))]
internal partial class RinhaSerializerContext : JsonSerializerContext { }