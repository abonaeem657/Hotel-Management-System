using hotel.Data;
using hotel.Models;
using hotel.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace hotel.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _users;
        private readonly SignInManager<ApplicationUser> _signIn;
        private readonly ApplicationDbContext _context;

        public AccountController(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn,
            ApplicationDbContext context)
        {
            _users = users;
            _signIn = signIn;
            _context = context;
        }

        [AllowAnonymous, HttpGet]
        public IActionResult Register() => View(new RegisterViewModel());

        [AllowAnonymous, HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid) return View(model);
            var user = new ApplicationUser
            {
                Name = model.Name.Trim(),
                UserName = model.Email.Trim(),
                Email = model.Email.Trim(),
                PhoneNumber = model.PhoneNumber.Trim()
            };

            // User creation and role assignment succeed together; never sign in a partially created account.
            await using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                var result = await _users.CreateAsync(user, model.Password);
                if (!result.Succeeded)
                {
                    foreach (var error in result.Errors)
                        ModelState.AddModelError(string.Empty, error.Description);
                    return View(model);
                }

                var roleResult = await _users.AddToRoleAsync(user, "Customer");
                if (!roleResult.Succeeded)
                {
                    ModelState.AddModelError(string.Empty, "Your account could not be created. Please try again.");
                    return View(model);
                }
                await transaction.CommitAsync();
            }

            await _signIn.SignInAsync(user, isPersistent: false);
            return RedirectToAction("Index", "CustomerRooms");
        }

        [AllowAnonymous, HttpGet]
        public IActionResult Login(string? returnUrl = null) =>
            View(new LoginViewModel { ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null });

        [AllowAnonymous, HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid) return View(model);
            var user = await _users.FindByEmailAsync(model.Email.Trim());
            if (user != null)
            {
                var result = await _signIn.PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: true);
                if (result.Succeeded)
                {
                    if (Url.IsLocalUrl(model.ReturnUrl)) return LocalRedirect(model.ReturnUrl!);
                    return await _users.IsInRoleAsync(user, "Admin")
                        ? RedirectToAction("Index", "Admin")
                        : RedirectToAction("Index", "CustomerRooms");
                }
            }

            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return View(model);
        }

        [Authorize, HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signIn.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        [AllowAnonymous, HttpGet]
        public IActionResult AccessDenied()
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return View();
        }
    }
}
