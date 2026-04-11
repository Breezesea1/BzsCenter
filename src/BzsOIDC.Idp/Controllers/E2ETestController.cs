using BzsOIDC.Idp.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BzsOIDC.Idp.Controllers;

[Route("testing/e2e")]
public sealed class E2ETestController(
    IConfiguration configuration,
    IWebHostEnvironment environment,
    SignInManager<BzsUser> signInManager,
    UserManager<BzsUser> userManager) : Controller
{
    [HttpGet("sign-in-admin")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> SignInAdmin([FromQuery] string? returnUrl = "/")
    {
        if (!environment.IsDevelopment() || !configuration.GetValue<bool>("Testing:E2E:Enabled"))
        {
            return NotFound();
        }

        var adminUserName = configuration["Identity:Admin:UserName"]?.Trim();
        if (string.IsNullOrWhiteSpace(adminUserName))
        {
            return NotFound();
        }

        var user = await userManager.FindByNameAsync(adminUserName);
        if (user is null)
        {
            return NotFound();
        }

        await signInManager.SignInAsync(user, isPersistent: false);

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return Redirect("/");
    }
}
