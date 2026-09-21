using hotel.Data;
using hotel.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hotel.Controllers
{
    [Authorize(Roles = "Admin")]
    public class RoomServicesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public RoomServicesController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var services = await _context.RoomServices.ToListAsync();

            return View(services);
        }
        // فتح صفحة إضافة خدمة
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        // حفظ الخدمة
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ServiceName")] RoomService roomService)
        {
            if (ModelState.IsValid)
            {
                _context.RoomServices.Add(roomService);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            return View(roomService);
        }
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var service = await _context.RoomServices.FindAsync(id);

            if (service == null)
            {
                return NotFound();
            }

            return View(service);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,ServiceName")] RoomService roomService)
        {
            if (id != roomService.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                var existing = await _context.RoomServices.FindAsync(id);
                if (existing == null) return NotFound();
                existing.ServiceName = roomService.ServiceName;
                try { await _context.SaveChangesAsync(); }
                catch (DbUpdateConcurrencyException) { return NotFound(); }

                return RedirectToAction(nameof(Index));
            }

            return View(roomService);
        }
        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            var service = await _context.RoomServices.FindAsync(id);

            if (service == null)
            {
                return NotFound();
            }

            return View(service);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var service = await _context.RoomServices.FindAsync(id);

            if (service == null)
            {
                return NotFound();
            }

            _context.RoomServices.Remove(service);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }
    }
}
