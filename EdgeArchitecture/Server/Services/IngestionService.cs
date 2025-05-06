using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Server.Models;
using System.Text.Json;
using System;
using Server.Db;
using Server.Configs;

namespace Server.Services
{
    public class IngestionService
    {
        private readonly ILogger<IngestionService> _logger;
        private readonly EdgeServerDbContext _dbContext;
        private readonly ServerConfigs _serverConfigs;

        public IngestionService(
            ILogger<IngestionService> logger,
            EdgeServerDbContext dbContext, // Inject DbContext
            IOptions<ServerConfigs> serverConfigs) // Inject Settings
        {
            _logger = logger;
            _dbContext = dbContext;
            _serverConfigs = serverConfigs.Value;
        }

        public async Task<bool> ProcessAndStoreDataAsync(GatewayDataRequest request)
        {
            var receivedTimestamp = DateTime.UtcNow;
            _logger.LogInformation("Processing data for DeviceId: {DeviceId}", request.DeviceId);

            try
            {
                // --- 1. Simulate Heavy Processing ---
                // Replace this with your actual complex logic if you have it
                // For now, we just delay to simulate work.
                _logger.LogDebug("Simulating heavy processing for {Duration} ms...", _serverConfigs.SimulatedProcessingMs);
                await Task.Delay(_serverConfigs.SimulatedProcessingMs);
                _logger.LogDebug("Simulated processing complete.");

                // --- 2. Simple Payload Transformation (Example) ---
                // Let's pretend we wrap the original payload with some metadata
                string processedPayload;
                try
                {
                    // Example: Add a server timestamp to the JSON payload
                    using var jsonDoc = JsonDocument.Parse(request.PayloadJson ?? "{}");
                    using var ms = new MemoryStream();
                    await using (var writer = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = false })) // Keep it compact
                    {
                        writer.WriteStartObject();
                        writer.WriteString("serverReceivedUtc", receivedTimestamp);
                        writer.WritePropertyName("originalPayload");
                        jsonDoc.RootElement.WriteTo(writer); // Write original payload nested
                        writer.WriteEndObject();
                    }
                    processedPayload = System.Text.Encoding.UTF8.GetString(ms.ToArray());
                }
                catch (JsonException jsonEx)
                {
                    _logger.LogWarning(jsonEx, "Payload for DeviceId {DeviceId} was not valid JSON. Storing raw.", request.DeviceId);
                    processedPayload = request.PayloadJson ?? string.Empty; // Store raw if not JSON
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during payload transformation for DeviceId {DeviceId}", request.DeviceId);
                    processedPayload = $"ERROR PROCESSING PAYLOAD: {ex.Message}"; // Store error indication
                }


                var processingCompletedTimestamp = DateTime.UtcNow;

                // --- 3. Create Database Record ---
                var dbRecord = new DataRecord
                {
                    DeviceId = request.DeviceId ?? "Unknown",
                    Topic = request.Topic,
                    ReceivedTimestamp = receivedTimestamp,
                    OriginalTimestamp = request.Timestamp, // Use timestamp from request
                    ProcessedPayload = processedPayload, // Store the transformed data
                    ProcessingCompletedTimestamp = processingCompletedTimestamp
                };

                // --- 4. Save to Database ---
                _dbContext.DataRecords.Add(dbRecord);
                await _dbContext.SaveChangesAsync(); // Asynchronously save changes

                _logger.LogInformation("Successfully processed and stored data for DeviceId: {DeviceId} (DB Id: {DbId})", dbRecord.DeviceId, dbRecord.Id);
                return true;
            }
            catch (DbUpdateException dbEx) // Catch specific DB errors
            {
                _logger.LogError(dbEx, "Database error storing data for DeviceId: {DeviceId}", request.DeviceId);
                // TODO: Add retry logic or dead-lettering if needed
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing data for DeviceId: {DeviceId}", request.DeviceId);
                return false;
            }
        }
    }
}
