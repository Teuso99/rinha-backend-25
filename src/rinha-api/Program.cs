using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Caching.Distributed;
using rinha_api;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var urlRedis = Environment.GetEnvironmentVariable("URL_REDIS") ?? "localhost";

urlRedis += ",abortConnect=false";

builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(urlRedis));

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
    throw new NotImplementedException();
});

app.MapPost("/payments", async (Guid correlationId, decimal amount) =>
{
    throw new NotImplementedException();
});

app.Run();

