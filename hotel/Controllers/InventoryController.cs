using hotel.Data;
using hotel.Models;
using hotel.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace hotel.Controllers;

[Authorize(Roles = "Admin")]
public class InventoryController(ApplicationDbContext context) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index() => View(await context.Inventories.AsNoTracking().OrderBy(x => x.ItemName).ToListAsync());

    [HttpGet]
    public IActionResult Create() => View("Edit", new InventoryInput());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(InventoryInput input)
    {
        if (!ModelState.IsValid) return View("Edit", input);
        context.Inventories.Add(new Inventory { ItemName = input.ItemName.Trim(), CurrentQuantity = input.CurrentQuantity!.Value, ReorderLevel = input.ReorderLevel!.Value });
        await context.SaveChangesAsync();
        TempData["Success"] = "Saved successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var item = await context.Inventories.FindAsync(id);
        if (item == null) return NotFound();
        return View(new InventoryInput { ItemName = item.ItemName, CurrentQuantity = item.CurrentQuantity, ReorderLevel = item.ReorderLevel });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, InventoryInput input)
    {
        var item = await context.Inventories.FindAsync(id);
        if (item == null) return NotFound();
        if (!ModelState.IsValid) return View(input);
        item.ItemName = input.ItemName.Trim();
        item.CurrentQuantity = input.CurrentQuantity!.Value;
        item.ReorderLevel = input.ReorderLevel!.Value;
        try { await context.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { return NotFound(); }
        TempData["Success"] = "Saved successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await context.Inventories.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
        return item == null ? NotFound() : View(item);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        if (await context.Inventories.Where(x => x.Id == id).ExecuteDeleteAsync() == 0) return NotFound();
        TempData["Success"] = "Deleted successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> UpdateQuantity(int id)
    {
        var item = await context.Inventories.FindAsync(id);
        if (item == null) return NotFound();
        ViewData["ItemName"] = item.ItemName;
        return View(new QuantityInput { CurrentQuantity = item.CurrentQuantity });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateQuantity(int id, QuantityInput input)
    {
        var item = await context.Inventories.FindAsync(id);
        if (item == null) return NotFound();
        ViewData["ItemName"] = item.ItemName;
        if (!ModelState.IsValid) return View(input);
        item.CurrentQuantity = input.CurrentQuantity!.Value;
        try { await context.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { return NotFound(); }
        TempData["Success"] = "Quantity updated.";
        return RedirectToAction(nameof(Index));
    }
}

