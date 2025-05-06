using Microsoft.AspNetCore.Mvc;
using Server.Models;
using Server.Services;

namespace Server.Controller
{
    [ApiController]
    [Route("api/ingest")] // Simple route
    public class IngestionController : ControllerBase
    {
        private readonly ILogger<IngestionController> _logger;
        private readonly IngestionService _ingestionService;

        public IngestionController(ILogger<IngestionController> logger, IngestionService ingestionService)
        {
            _logger = logger;
            _ingestionService = ingestionService;
        }

        [HttpPost]
        [ProducesResponseType(StatusCodes.Status202Accepted)] // Indicate request accepted for processing
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> PostIngestData([FromBody] GatewayDataRequest dataRequest)
        {
            // Basic validation provided by [ApiController] and model attributes ([Required])
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid ingestion request received: {ModelState}", ModelState);
                return BadRequest(ModelState);
            }

            try
            {
                _logger.LogInformation("API endpoint received data for DeviceId: {DeviceId}", dataRequest.DeviceId);

                // Offload processing and storage to the service
                bool success = await _ingestionService.ProcessAndStoreDataAsync(dataRequest);

                if (success)
                {
                    // Accepted: Request is valid and processing initiated (or completed quickly)
                    return Accepted();
                }
                else
                {
                    // If the service explicitly returns false, it means processing/storage failed
                    return StatusCode(StatusCodes.Status500InternalServerError, "Failed to process or store data.");
                }
            }
            catch (Exception ex)
            {
                // Catch unexpected errors during the handoff to the service (should be rare)
                _logger.LogError(ex, "Unexpected error in API endpoint for DeviceId: {DeviceId}", dataRequest.DeviceId);
                return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
            }
        }
    }
}
