using hotel.Data;
using hotel.Models;
using hotel.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hotel.Controllers;

[Authorize(Roles = "Admin")]
public class OffersController(ApplicationDbContext context) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index() => View(await context.Offers.AsNoTracking().OrderBy(x => x.MinimumNights).ToListAsync());

    [HttpGet]
    public IActionResult Create() => View("Edit", new OfferInput());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(OfferInput input)
    {
        if (!ModelState.IsValid) return View("Edit", input);
        context.Offers.Add(new Offer { MinimumNights = input.MinimumNights!.Value, DiscountPercentage = input.DiscountPercentage!.Value });
        await context.SaveChangesAsync();
        TempData["Success"] = "Saved successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var item = await context.Offers.FindAsync(id);
        if (item == null) return NotFound();
        return View(new OfferInput { MinimumNights = item.MinimumNights, DiscountPercentage = item.DiscountPercentage });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, OfferInput input)
    {
        var item = await context.Offers.FindAsync(id);
        if (item == null) return NotFound();
        if (!ModelState.IsValid) return View(input);
        item.MinimumNights = input.MinimumNights!.Value;
        item.DiscountPercentage = input.DiscountPercentage!.Value;
        try { await context.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { return NotFound(); }
        TempData["Success"] = "Saved successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await context.Offers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
        return item == null ? NotFound() : View(item);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        if (await context.Offers.Where(x => x.Id == id).ExecuteDeleteAsync() == 0) return NotFound();
        TempData["Success"] = "Deleted successfully.";
        return RedirectToAction(nameof(Index));
    }
}

