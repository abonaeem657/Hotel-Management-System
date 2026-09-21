using hotel.Data;
using hotel.Models;
using hotel.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace hotel.Controllers;

[AllowAnonymous]
public class ContactController(ApplicationDbContext context) : Controller
{
    [HttpGet]
    public IActionResult Index() => View(new ContactInput());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(ContactInput input)
    {
        if (!ModelState.IsValid) return View(input);
        context.ContactMessages.Add(new ContactMessage
        {
            SenderName = input.SenderName.Trim(), Email = input.Email.Trim(),
            MessageBody = input.MessageBody.Trim(), IsRead = false, SentDate = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        TempData["Success"] = "Your message has been sent. Thank you for contacting us.";
        return RedirectToAction(nameof(Index));
    }
}
