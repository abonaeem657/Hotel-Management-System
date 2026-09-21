using hotel.Data;
using hotel.Models;
using hotel.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hotel.Controllers
{
    [Authorize(Roles = "Admin")]
    public class RoomsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public RoomsController(ApplicationDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        //(select * from rooms) 
        public async Task<IActionResult> Index()
        {
            var rooms = await _context.Rooms
                .AsNoTracking()
                .Include(r => r.RoomServices)
                .Include(r => r.RoomImages)
                .AsSplitQuery()
                .ToListAsync();

            ViewData["AvailableServices"] = await _context.RoomServices
                .AsNoTracking().OrderBy(s => s.ServiceName).ToListAsync();

            return View(rooms);
        }
        // فتح صفحة إضافة غرفة
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        // حفظ الغرفة في قاعدة البيانات
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("RoomNumber,RoomType,BasePricePerNight,Description,IsAvailable")] Room room,
            bool modalRequest = false)
        {
            if (ModelState.IsValid)
            {
                _context.Rooms.Add(room);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            if (modalRequest)
            {
                return ModalValidationErrors();
            }

            return View(room);
        }
        // فتح صفحة تعديل الغرفة
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var room = await _context.Rooms.FindAsync(id);

            if (room == null)
            {
                return NotFound();
            }

            return View(room);
        }


        // حفظ التعديلات
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id,
            [Bind("Id,RoomNumber,RoomType,BasePricePerNight,Description,IsAvailable")] Room room,
            bool modalEdit = false)
        {
            if (id != room.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                var existingRoom = await _context.Rooms.FindAsync(id);

                if (existingRoom == null)
                {
                    return NotFound();
                }

                existingRoom.RoomNumber = room.RoomNumber;
                existingRoom.RoomType = room.RoomType;
                existingRoom.BasePricePerNight = room.BasePricePerNight;
                existingRoom.Description = room.Description;
                existingRoom.IsAvailable = room.IsAvailable;
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            if (modalEdit)
            {
                return BadRequest(new
                {
                    errors = ModelState.Values
                        .SelectMany(value => value.Errors)
                        .Select(error => string.IsNullOrEmpty(error.ErrorMessage)
                            ? "Please enter a valid value."
                            : error.ErrorMessage)
                        .ToArray()
                });
            }

            return View(room);
        }
        // فتح صفحة تأكيد الحذف
        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            var room = await _context.Rooms.FindAsync(id);

            if (room == null)
            {
                return NotFound();
            }

            return View(room);
        }

        // تأكيد حذف الغرفة
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id, bool modalRequest = false)
        {
            // Protect booking history from the existing database's cascade delete.
            // Match booking creation's room-first lock to prevent a concurrent insert.
            await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var room = await _context.Rooms.FromSqlInterpolated(
                $"SELECT * FROM [Rooms] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {id}").SingleOrDefaultAsync();

            if (room == null)
            {
                return NotFound();
            }

            if (await _context.Bookings.AnyAsync(b => b.RoomId == id))
            {
                const string error = "This room has booking history and cannot be deleted. Disable it instead.";
                if (modalRequest) return BadRequest(new { errors = new[] { error } });
                ModelState.AddModelError(string.Empty, error);
                return View("Delete", room);
            }

            _context.Rooms.Remove(room);
            try
            {
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (DbUpdateException)
            {
                const string error = "This room could not be deleted. It may have related bookings or other records.";
                if (modalRequest) return BadRequest(new { errors = new[] { error } });
                ModelState.AddModelError(string.Empty, error);
                return View("Delete", room);
            }

            return RedirectToAction(nameof(Index));
        }
        // عرض تفاصيل الغرفة
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var room = await _context.Rooms.FindAsync(id);

            if (room == null)
            {
                return NotFound();
            }

            return View(room);
        }
        [HttpGet]
        public async Task<IActionResult> ManageServices(int id)
        {
            var room = await _context.Rooms
                .Include(r => r.RoomServices)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (room == null)
            {
                return NotFound();
            }

            var viewModel = new RoomServicesViewModel
            {
                RoomId = room.Id,
                RoomNumber = room.RoomNumber,

                SelectedServiceIds = room.RoomServices
                    .Select(s => s.Id)
                    .ToList(),

                AvailableServices = await _context.RoomServices
                    .ToListAsync()
            };

            return View(viewModel);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ManageServices(RoomServicesViewModel model, bool modalRequest = false)
        {
            var room = await _context.Rooms
                .Include(r => r.RoomServices)
                .FirstOrDefaultAsync(r => r.Id == model.RoomId);

            if (room == null)
            {
                return NotFound();
            }

            var selectedServices = await _context.RoomServices
                .Where(s => model.SelectedServiceIds.Contains(s.Id))
                .ToListAsync();

            if (selectedServices.Count != model.SelectedServiceIds.Distinct().Count())
            {
                ModelState.AddModelError(nameof(model.SelectedServiceIds), "One or more selected services no longer exist. Please refresh the page.");
            }

            if (!ModelState.IsValid)
            {
                if (modalRequest)
                {
                    return ModalValidationErrors();
                }

                model.RoomNumber = room.RoomNumber;
                model.AvailableServices = await _context.RoomServices.ToListAsync();
                return View(model);
            }

            room.RoomServices.Clear();

            foreach (var service in selectedServices)
            {
                room.RoomServices.Add(service);
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }
        [HttpGet]
        public async Task<IActionResult> ManageImages(int id)
        {
            var room = await _context.Rooms
                .Include(r => r.RoomImages)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (room == null)
            {
                return NotFound();
            }

            return View(room);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadImage(int roomId, IFormFile? imageFile, bool modalRequest = false)
        {
            var room = await _context.Rooms.FindAsync(roomId);

            if (room == null)
            {
                return NotFound();
            }

            if (imageFile == null || imageFile.Length == 0)
            {
                return ImageResult(roomId, modalRequest, "Please select an image.");
            }

            // Maximum size: 5 MB
            if (imageFile.Length > 5 * 1024 * 1024)
            {
                return ImageResult(roomId, modalRequest, "Image size must not exceed 5 MB.");
            }

            // Allowed extensions
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };

            var extension = Path.GetExtension(imageFile.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(extension))
            {
                return ImageResult(roomId, modalRequest, "Only JPG, JPEG, PNG and WEBP images are allowed.");
            }

            if (!ModelState.IsValid)
            {
                return ImageResult(roomId, modalRequest, "Please provide a valid image and room.");
            }

            var fileName = Guid.NewGuid().ToString() + extension;

            var folderPath = Path.Combine(
                _environment.WebRootPath,
                "images",
                "rooms");

            Directory.CreateDirectory(folderPath);

            var filePath = Path.Combine(folderPath, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await imageFile.CopyToAsync(stream);
            }

            var roomImage = new RoomImage
            {
                RoomId = roomId,
                ImagePath = "/images/rooms/" + fileName
            };

            _context.RoomImages.Add(roomImage);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Image uploaded successfully.";

            return ImageResult(roomId, modalRequest);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteImage(int imageId, int roomId, bool modalRequest = false)
        {
            var image = await _context.RoomImages.FindAsync(imageId);

            if (image == null || image.RoomId != roomId)
            {
                return NotFound();
            }

            var folderPath = Path.GetFullPath(Path.Combine(_environment.WebRootPath, "images", "rooms"));
            var imagePath = Path.GetFullPath(Path.Combine(_environment.WebRootPath,
                image.ImagePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));
            if (!imagePath.StartsWith(folderPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                return ImageResult(roomId, modalRequest, "The image path is invalid.");

            if (System.IO.File.Exists(imagePath))
            {
                System.IO.File.Delete(imagePath);
            }

            _context.RoomImages.Remove(image);
            await _context.SaveChangesAsync();

            return ImageResult(roomId, modalRequest);
        }

        private IActionResult ModalValidationErrors()
        {
            return BadRequest(new
            {
                errors = ModelState.Values.SelectMany(value => value.Errors)
                    .Select(error => string.IsNullOrEmpty(error.ErrorMessage)
                        ? "Please enter a valid value."
                        : error.ErrorMessage)
                    .ToArray()
            });
        }

        private IActionResult ImageResult(int roomId, bool modalRequest, string? error = null)
        {
            if (error != null)
            {
                if (modalRequest)
                {
                    return BadRequest(new { errors = new[] { error } });
                }

                TempData["Error"] = error;
            }

            return modalRequest
                ? RedirectToAction(nameof(Index))
                : RedirectToAction(nameof(ManageImages), new { id = roomId });
        }
    }
}
