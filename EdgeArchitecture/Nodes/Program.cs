using Nodes.Background;
using Nodes.Configs;
using Nodes.Models;
using Nodes.ProcessData;
using Shared.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<NodeConfigs>(builder.Configuration.GetSection("NodeConfigs"));
builder.Services.Configure<ProcessingConfigs>(builder.Configuration.GetSection("ProcessingConfigs"));

// --- Shared MQTT Communication ---
// Load MqttOptions from "Mqtt" section AND potentially override Password/ClientId from secure sources/NodeSettings
builder.Services.AddMqttCommunicator(builder.Configuration, "MqttConfigs");

// --- Internal Processing Queue ---
builder.Services.AddSingleton<ProcessingChannel>(); // Singleton holds the channel

// --- Data Processors (Register interface and implementations) ---
// Use Scoped lifetime: each message processing gets its own instance/scope
builder.Services.AddSingleton<IDataProcessor, SensorDataProcessor>();
//builder.Services.AddScoped<IDataProcessor, GatewayCommandProcessor>();
// Add more processors as needed...

// --- Service that manages the concurrent processors ---
builder.Services.AddHostedService<MessageProcessingService>(); // Runs the consumers

// --- Main Worker Service (Handles MQTT interaction) ---
builder.Services.AddHostedService<NodesWorkerService>();


var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.Run();
