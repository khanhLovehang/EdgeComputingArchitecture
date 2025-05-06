using Gateway.Configs;
using Microsoft.Extensions.Options;
using System.Runtime;
using System.Text.Json;
using System.Text;
using System.Threading.Channels;
using Gateway.Models;
using Gateway.Requests;
using System.Net.Http.Json;

namespace Gateway.Background
{
    public class ForwardingService : BackgroundService
    {
        private readonly ILogger<ForwardingService> _logger;
        private readonly ProcessingChannel _processingChannel;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly GatewayConfigs _gatewayConfigs;

        public ForwardingService(
           ILogger<ForwardingService> logger,
           ProcessingChannel processingChannel,
           IHttpClientFactory httpClientFactory,
           IOptions<GatewayConfigs> gatewayConfigs)
        {
            _logger = logger;
            _processingChannel = processingChannel;
            _httpClientFactory = httpClientFactory;
            _gatewayConfigs = gatewayConfigs.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Forwarding Service started. Waiting for messages to forward to {ServerUrl}", _gatewayConfigs.ServerBaseUrl);

            try
            {
                // Continuously read from the channel
                await foreach (var message in _processingChannel.Reader.ReadAllAsync(stoppingToken))
                {
                    try
                    {
                        _logger.LogDebug("Dequeued message from topic {Topic}. Forwarding to server...", message.Topic);

                        var httpClient = _httpClientFactory.CreateClient("ServerClient");

                        // --- Prepare Content ---
                        // Assumption: Server expects JSON. Try to deserialize, otherwise send raw string.
                        // You MUST adapt this based on what your Server API actually expects!
                        HttpContent? content = null;
                        string payloadString = Encoding.UTF8.GetString(message.Payload);

                        var deserializedPayload = JsonSerializer.Deserialize<Dictionary<string, object>>(payloadString);

                        // 4. Convert to GatewayDataRequest
                        var apiRequest = new GatewayDataRequest
                        {
                            DeviceId = deserializedPayload["deviceId"].ToString(),
                            Topic = $"nodes/{deserializedPayload["deviceId"]}/{deserializedPayload["sensorType"]}/data/processed",
                            Timestamp = DateTime.Parse(deserializedPayload["timestamp"].ToString()), // Ensure DateTime conversion
                            PayloadJson = JsonSerializer.Serialize(deserializedPayload) // Back to JSON string
                        };

                        //try
                        //{
                        //    // Attempt to parse as JSON and send as JSON content type
                        //    using var jsonDoc = JsonDocument.Parse(payloadString);
                        //    content = JsonContent.Create(jsonDoc.RootElement.Clone()); // Send the parsed JSON
                        //    _logger.LogTrace("Forwarding payload as application/json");
                        //}
                        //catch (JsonException)
                        //{
                        //    // If not valid JSON, send as plain text
                        //    content = new StringContent(apiRequest, Encoding.UTF8, "text/plain");
                        //    _logger.LogTrace("Forwarding payload as text/plain (was not valid JSON)");
                        //}


                        // --- Prepare Request ---
                        //using var request = new HttpRequestMessage(HttpMethod.Post, _gatewayConfigs.ServerBaseUrl)
                        //{
                        //    Content = content
                        //};

                        //// Add API Key header if configured
                        //if (!string.IsNullOrWhiteSpace(_gatewayConfigs.ServerApiKey))
                        //{
                        //    // Adjust header name ("X-Api-Key") if needed by your server
                        //    request.Headers.TryAddWithoutValidation("X-Api-Key", _gatewayConfigs.ServerApiKey);
                        //}

                        // --- Send Request ---
                        var response = await httpClient.PostAsJsonAsync(_gatewayConfigs.ServerBaseUrl, apiRequest);

                        if (response.IsSuccessStatusCode)
                        {
                            _logger.LogInformation("Successfully forwarded message from topic {Topic} to server. Status: {StatusCode}", message.Topic, response.StatusCode);
                        }
                        else
                        {
                            string errorDetail = await response.Content.ReadAsStringAsync(stoppingToken);
                            _logger.LogError("Failed to forward message from topic {Topic} to server. Status: {StatusCode}, Reason: {Reason}, Detail: {Detail}",
                                message.Topic, response.StatusCode, response.ReasonPhrase, errorDetail);
                            // NOTE: No retry logic in this simple version. Add if needed.
                        }
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        _logger.LogInformation("Forwarding loop cancelled for message from {Topic}.", message?.Topic);
                        break; // Exit loop
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error forwarding message from topic {Topic}.", message?.Topic ?? "N/A");
                        // Consider delaying briefly before processing next message on error
                        await Task.Delay(1000, stoppingToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Forwarding service main loop cancelled.");
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Forwarding service failed unexpectedly.");
            }
            _logger.LogInformation("Forwarding Service stopped.");
        }
    }
}
