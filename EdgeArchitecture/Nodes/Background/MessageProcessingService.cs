using Microsoft.Extensions.Options;
using Nodes.Configs;
using Nodes.Models;
using Nodes.ProcessData;
using Shared.Communication;

namespace Nodes.Background
{
    public class MessageProcessingService : BackgroundService
    {
        private readonly ILogger<MessageProcessingService> _logger;
        private readonly NodeConfigs _nodeConfigs;
        private readonly ProcessingChannel _channel;
        private readonly IServiceProvider _serviceProvider;
        private readonly IDataProcessor _dataProcessor;

        public MessageProcessingService(
            ILogger<MessageProcessingService> logger
            , IOptions<NodeConfigs> nodeConfigs
            , ProcessingChannel channel
            , IServiceProvider serviceProvider
            , IDataProcessor dataProcessor
            ) // To create scopes for processors
        {
            _logger = logger;
            _nodeConfigs = nodeConfigs.Value;
            _channel = channel;
            _serviceProvider = serviceProvider;
            _dataProcessor = dataProcessor;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            int parallelism = _nodeConfigs.ProcessingParallelism;
            _logger.LogInformation("Starting Message Processing Service with {Parallelism} concurrent consumers.", parallelism);

            var consumerTasks = new List<Task>(parallelism);

            for (int i = 0; i < parallelism; i++)
            {
                int consumerId = i + 1;
                consumerTasks.Add(RunConsumerLoopAsync(consumerId, stoppingToken));
            }

            await Task.WhenAll(consumerTasks);

            _logger.LogInformation("Message Processing Service stopped.");
        }

        private async Task RunConsumerLoopAsync(int consumerId, CancellationToken stoppingToken)
        {
            _logger.LogInformation("Message consumer {ConsumerId} started.", consumerId);

            try
            {
                // Continuously read from the channel until it's completed and empty
                await foreach (var message in _channel.Reader.ReadAllAsync(stoppingToken))
                {
                    try
                    {
                        _logger.LogDebug("Consumer {ConsumerId} processing message from topic {Topic}", consumerId, message.Topic);

                        // Create a DI scope for each message processing operation
                        // This allows Scoped services (like DB contexts or specific processors)
                        using var scope = _serviceProvider.CreateScope();

                        // Resolve all registered data processors within the scope
                        var processors = scope.ServiceProvider.GetServices<IDataProcessor>();

                        bool processed = false;
                        foreach (var processor in processors)
                        {
                            // Check if this processor handles the message topic
                            //if (processor.CanProcess(message.Topic))
                            //{
                            await _dataProcessor.ProcessMessageAsync(message, stoppingToken);
                            processed = true;
                            // Decide if multiple processors can handle the same message or just the first one
                            // break; // Uncomment if only one processor should handle a message
                            //}
                        }

                        if (!processed)
                        {
                            _logger.LogWarning("Consumer {ConsumerId}: No processor found for topic {Topic}", consumerId, message.Topic);
                        }
                        _logger.LogDebug("Consumer {ConsumerId} finished processing message from topic {Topic}", consumerId, message.Topic);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        _logger.LogInformation("Consumer {ConsumerId} stopping due to cancellation.", consumerId);
                        break; // Exit loop gracefully
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Consumer {ConsumerId}: Error processing message from topic {Topic}.", consumerId, message?.Topic ?? "N/A");
                        // Implement error handling strategy: Dead-letter queue? Log and continue?
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Consumer {ConsumerId} read loop cancelled.", consumerId);
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Consumer {ConsumerId} loop failed unexpectedly.", consumerId);
            }
            finally
            {
                _logger.LogInformation("Message consumer {ConsumerId} finished.", consumerId);
            }
        }
    }
}
