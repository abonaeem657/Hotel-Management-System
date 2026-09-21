using hotel.Data;
using hotel.Models;
using hotel.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hotel.Controllers;

[Authorize(Roles = "Admin")]
public class SeasonPricingController(ApplicationDbContext context) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index() => View(await context.SeasonPricings.AsNoTracking().OrderBy(x => x.StartDate).ToListAsync());

    [HttpGet]
    public IActionResult Create() => View("Edit", new SeasonPricingInput());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SeasonPricingInput input)
    {
        if (!ModelState.IsValid) return View("Edit", input);
        context.SeasonPricings.Add(new SeasonPricing { SeasonName = input.SeasonName.Trim(), StartDate = input.StartDate!.Value.Date,
            EndDate = input.EndDate!.Value.Date, PriceMultiplier = input.PriceMultiplier!.Value });
        await context.SaveChangesAsync();
        TempData["Success"] = "Saved successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var item = await context.SeasonPricings.FindAsync(id);
        if (item == null) return NotFound();
        return View(new SeasonPricingInput { SeasonName = item.SeasonName, StartDate = item.StartDate, EndDate = item.EndDate, PriceMultiplier = item.PriceMultiplier });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, SeasonPricingInput input)
    {
        var item = await context.SeasonPricings.FindAsync(id);
        if (item == null) return NotFound();
        if (!ModelState.IsValid) return View(input);
        item.SeasonName = input.SeasonName.Trim();
        item.StartDate = input.StartDate!.Value.Date;
        item.EndDate = input.EndDate!.Value.Date;
        item.PriceMultiplier = input.PriceMultiplier!.Value;
        try { await context.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { return NotFound(); }
        TempData["Success"] = "Saved successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await context.SeasonPricings.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
        return item == null ? NotFound() : View(item);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        if (await context.SeasonPricings.Where(x => x.Id == id).ExecuteDeleteAsync() == 0) return NotFound();
        TempData["Success"] = "Deleted successfully.";
        return RedirectToAction(nameof(Index));
    }
}

