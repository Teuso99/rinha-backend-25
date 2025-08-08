using System.Runtime;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using rinha_api;
using rinha_api.DTO;
using rinha_api.Model;
using rinha_api.Repository;
using StackExchange.Redis;

var builder = WebApplication.CreateSlimBuilder(args);

var urlRedis = Environment.GetEnvironmentVariable("URL_REDIS") ?? "localhost";
urlRedis += ",abortConnect=false";

builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(urlRedis));

builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();

builder.Services.AddHostedService<ProcessPaymentService>();

builder.Services.ConfigureHttpJsonOptions(options => {
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, RinhaSerializerContext.Default);
    options.SerializerOptions.DefaultBufferSize = 256;
});

builder.Logging.ClearProviders();
GCSettings.LatencyMode = GCLatencyMode.LowLatency;

var app = builder.Build();

app.UseRouting();

app.MapGet("/payments-summary", async (DateTime? from, DateTime? to, [FromServices] IPaymentRepository paymentRepository) =>
{
    var paymentsByDefault = await paymentRepository.GetPaymentsByProcessorAsync("default", from, to);
    var paymentsByFallback = await paymentRepository.GetPaymentsByProcessorAsync("fallback", from, to);
    
    return Results.Ok(new PaymentsDTO(paymentsByDefault, paymentsByFallback));
});

app.MapPost("/payments", async (PaymentRequestDTO request, [FromServices] IConnectionMultiplexer redis) =>
{
    var queue = redis.GetDatabase();
    
    var payment = new Payment(request.CorrelationId, request.Amount, string.Empty);
    
    var result = await queue.ListRightPushAsync("payments", 
                                                    JsonSerializer.Serialize(payment, 
                                                                                RinhaSerializerContext.Default.Payment));

    return result > 0 ? Results.Created() : Results.BadRequest();
});

app.Run();

record PaymentRequestDTO(Guid CorrelationId, decimal Amount);

[JsonSerializable(typeof(HealthcheckDTO))]
[JsonSerializable(typeof(PaymentRequestDTO))]
[JsonSerializable(typeof(PaymentsDTO))]
[JsonSerializable(typeof(Payment))]
internal partial class RinhaSerializerContext : JsonSerializerContext { }