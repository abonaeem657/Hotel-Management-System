using hotel.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hotel.Controllers;

[Authorize(Roles = "Admin")]
public class InboxController(ApplicationDbContext context) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index() => View(await context.ContactMessages.AsNoTracking()
        .OrderBy(m => m.IsRead).ThenByDescending(m => m.SentDate).ThenByDescending(m => m.Id).ToListAsync());

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var message = await context.ContactMessages.AsNoTracking().SingleOrDefaultAsync(m => m.Id == id);
        return message == null ? NotFound() : View(message);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        var changed = await context.ContactMessages.Where(m => m.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.IsRead, true));
        if (changed == 0) return NotFound();
        TempData["Success"] = "Message marked as read.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        var message = await context.ContactMessages.AsNoTracking().SingleOrDefaultAsync(m => m.Id == id);
        return message == null ? NotFound() : View(message);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        if (await context.ContactMessages.Where(m => m.Id == id).ExecuteDeleteAsync() == 0) return NotFound();
        TempData["Success"] = "Message deleted.";
        return RedirectToAction(nameof(Index));
    }
}
