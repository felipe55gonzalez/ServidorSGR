using Microsoft.AspNetCore.SignalR;
using ServidorSGR.Hubs;
using ServidorSGR.Services;
using MessagePack; 
using Microsoft.AspNetCore.Builder; 
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting; 

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IConnectionMapping, ConnectionMapping>();

builder.Services.AddSignalR(options =>
{

    options.MaximumReceiveMessageSize = 10 * 1024 * 1024; 


})
.AddMessagePackProtocol(); 

var app = builder.Build();

app.MapHub<DataHub>("/datahub");

app.MapGet("/", async context =>
{
    await context.Response.WriteAsync("ServidorSGR is running.");
});

app.Run();