using DevicesDashboard.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System;

namespace DevicesDashboard.Controllers
{
    [ApiController]
    [Route("api/v1")]
    public class SensorApiController : ControllerBase
    {
        private readonly EdgeDevicesDbContext _context;

        public SensorApiController(EdgeDevicesDbContext context)
        {
            _context = context;
        }

        [HttpGet("devices")]
        public async Task<IActionResult> GetDevices()
        {
            var sensors = await _context.Devices.Where(s => s.IsActive && s.IsDeleted == false).ToListAsync();

            return Ok(sensors);
        }

        [HttpPost("devices/ping/{deviceId}")]
        public async Task<IActionResult> Ping(long deviceId)
        {
            var sensor = await _context.Devices.FirstOrDefaultAsync(s => s.Id == deviceId);
            if (sensor == null) return NotFound();

            sensor.LastPing = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok("Ping success!");
        }
    }
}
