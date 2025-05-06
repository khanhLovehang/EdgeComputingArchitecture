using Microsoft.EntityFrameworkCore;
using Server.Configs;
using Server.Db;
using Server.Services;

var builder = WebApplication.CreateBuilder(args);
// --- Configuration ---
var configuration = builder.Configuration;
builder.Services.Configure<ServerConfigs>(builder.Configuration.GetSection("ServerConfigs"));

// --- Logging ---
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// --- Add services to the container ---
builder.Services.AddControllers();

// --- Configure DbContext ---
var connectionString = builder.Configuration.GetConnectionString("EdgeServerDBConnection");
if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("Connection string 'AppDatabase' not found or is empty. Configure it in appsettings.json or User Secrets.");
}
builder.Services.AddDbContext<EdgeServerDbContext>(options =>
    options.UseSqlServer(connectionString, sqlServerOptionsAction: sqlOptions =>
    {
        // Optional: Enable retry logic for transient SQL Server errors
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null);
    }));


// --- Register Application Services ---
builder.Services.AddScoped<IngestionService>(); // Use Scoped lifetime for services using DbContext

// --- Swagger/OpenAPI (Optional but helpful for testing) ---
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// --- Build App ---
var app = builder.Build();

// --- Configure the HTTP request pipeline. ---
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseDeveloperExceptionPage(); // Show detailed errors in dev
}

// app.UseHttpsRedirection(); // Enable HTTPS - IMPORTANT for production

// app.UseAuthorization(); // Add if you implement authentication/authorization

app.MapControllers(); // Map API controllers

//// --- Automatically apply migrations on startup (Optional - Good for dev/simple scenarios) ---
//// WARNING: Review carefully for production use. Consider dedicated migration strategies.
//try
//{
//    _ = ApplyMigrations(app.Services);
//}
//catch (Exception ex)
//{
//    var logger = app.Services.GetRequiredService<ILogger<Program>>();
//    logger.LogCritical(ex, "An error occurred while migrating the database.");
//    // Optionally prevent startup if migration fails
//    // return;
//}

app.Run();
