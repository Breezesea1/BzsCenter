using BzsCenter.Idp.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BzsCenter.Idp.Controllers;

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

        var user = await userManager.FindByNameAsync("admin");
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
