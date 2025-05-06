using Gateway.Background;
using Gateway.Configs;
using Gateway.Models;
using Shared.Extensions;

var builder = WebApplication.CreateBuilder(args);

// --- Configuration ---
builder.Services.Configure<GatewayConfigs>(builder.Configuration.GetSection("GatewayConfigs"));
builder.Services.AddOptions();

// --- Shared MQTT Communication ---
builder.Services.AddMqttCommunicator(builder.Configuration, "MqttConfigs");

// --- Internal Processing Queue ---
builder.Services.AddSingleton<ProcessingChannel>();

// --- HTTP Client Factory ---
builder.Services.AddHttpClient("ServerClient"); // Named client for server communication

// --- Background builder.Services ---
builder.Services.AddHostedService<GatewayWokerService>();       // Handles MQTT Comms
builder.Services.AddHostedService<ForwardingService>();   // Handles HTTP Forwarding

var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.Run();
