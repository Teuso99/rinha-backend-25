using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using rinha_api;
using rinha_api.Context;
using rinha_api.DTO;
using rinha_api.Model;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

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

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();


app.MapGet("/payments-summary", (DateTime? from, DateTime? to) =>
{
    var paymentsByDefault = app.Services.GetRequiredService<RinhaContext>().PaymentsByDefault.Where(p =>
                                                (from == null || p.RequestedAt >= from.Value) &&
                                                (to == null || p.RequestedAt <= to.Value)).ToList();
    
    var paymentsByFallback = app.Services.GetRequiredService<RinhaContext>().PaymentsByFallback.Where(p =>
                                                (from == null || p.RequestedAt >= from.Value) &&
                                                (to == null || p.RequestedAt <= to.Value)).ToList();
    
    return Results.Ok(new PaymentsDTO(paymentsByDefault, paymentsByFallback));
});

app.MapPost("/payments", async (Guid correlationId, decimal amount, [FromServices] IConnectionMultiplexer redis) =>
{
    var queue = redis.GetDatabase();
    
    var payment = new Payment(correlationId, amount);
    
    var result = await queue.ListRightPushAsync("payments", JsonSerializer.Serialize(payment));

    return result > 0 ? Results.Created() : Results.BadRequest();
});

app.Run();

