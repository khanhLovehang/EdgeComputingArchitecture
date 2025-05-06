using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using DevicesDashboard.Models;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace DevicesDashboard.Controllers
{
    public class DevicesController : Controller
    {
        private readonly EdgeDevicesDbContext _context;

        public DevicesController(EdgeDevicesDbContext context)
        {
            _context = context;
        }

        // GET: Devices
        public async Task<IActionResult> Index()
        {
            return View(await _context.Devices.Where(item => item.IsDeleted == false).ToListAsync());
        }

        // GET: Devices/Details/5
        public async Task<IActionResult> Details(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var device = await _context.Devices
                .FirstOrDefaultAsync(m => m.Id == id && m.IsDeleted == false);
            if (device == null)
            {
                return NotFound();
            }

            return View(device);
        }

        // GET: Devices/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Devices/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,IsDeleted,Name,SensorType,IsActive,IsRunning,LastPing,CreatedDate,UpdatedDate,DeletedDate")] Device device)
        {
            if (ModelState.IsValid)
            {
                device.CreatedDate = DateTime.Now;
                _context.Add(device);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(device);
        }

        // GET: Devices/Edit/5
        public async Task<IActionResult> Edit(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var device = await _context.Devices.FirstOrDefaultAsync(m => m.Id == id && m.IsDeleted == false);
            if (device == null)
            {
                return NotFound();
            }
            return View(device);
        }

        // POST: Devices/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(long id, [Bind("Id,IsDeleted,Name,SensorType,IsActive,IsRunning,LastPing,CreatedDate,UpdatedDate,DeletedDate")] Device device)
        {
            if (id != device.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {

                    device.UpdatedDate = DateTime.Now;
                    _context.Update(device);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!DeviceExists(device.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(device);
        }

        // GET: Devices/Delete/5
        public async Task<IActionResult> Delete(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var device = await _context.Devices
                .FirstOrDefaultAsync(m => m.Id == id && m.IsDeleted == false);
            if (device == null)
            {
                return NotFound();
            }

            return View(device);
        }

        // POST: Devices/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(long id)
        {
            var device = await _context.Devices.FindAsync(id);
            if (device != null)
            {
                device.DeletedDate = DateTime.Now;
                device.IsDeleted = true;
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool DeviceExists(long id)
        {
            return _context.Devices.Any(e => e.Id == id && e.IsDeleted == false);
        }

        // Start device

        [HttpPost, ActionName("StartDevice")]
        public async Task StartDevice(long id)
        {
            //if (id < 0)
            //{
            //    return BadRequest("DeviceId invalid!");
            //}

            var device = await _context.Devices.FindAsync(id);
            if (device != null)
            {
                device.IsActive = true;
                await _context.SaveChangesAsync();
            }

           

           //return View("Index");
        }

        // Stop device
        [HttpPost, ActionName("StopDevice")]
        public async Task StopDevice(long id)
        {
            var device = await _context.Devices.FindAsync(id);
            if (device != null)
            {
                device.IsActive = false;
                await _context.SaveChangesAsync();
            }
        }
    }
}
