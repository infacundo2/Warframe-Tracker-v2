using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace WarframeInventory.Controllers
{
    public class AuthMvcController : Controller
    {
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly UserManager<IdentityUser> _userManager;

        public AuthMvcController(SignInManager<IdentityUser> signInManager, UserManager<IdentityUser> userManager)
        {
            _signInManager = signInManager;
            _userManager = userManager;
        }

        [HttpPost("/auth/login-mvc")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> LoginMvc(string username, string password, bool rememberMe = false)
        {
            var user = await _userManager.FindByNameAsync(username);
            if (user == null)
                return Redirect("/auth/login?error=Credenciales%20invalidas");

            var result = await _signInManager.PasswordSignInAsync(user, password, rememberMe, true);
            if (!result.Succeeded)
                return Redirect("/auth/login?error=Credenciales%20invalidas");
            return Redirect("/");
        }

        [HttpPost("/auth/register-mvc")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> RegisterMvc(string username, string email, string password)
        {
            if (string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password))
                return Redirect("/auth/register?error=Campos%20incompletos");

            var existing = await _userManager.FindByNameAsync(username);
            var existingEmail = await _userManager.FindByEmailAsync(email);
            if (existing != null || existingEmail != null)
                return Redirect("/auth/register?error=No%20se%20pudo%20crear%20la%20cuenta");

            var user = new IdentityUser { UserName = username, Email = email };
            var result = await _userManager.CreateAsync(user, password);

            if (!result.Succeeded)
            {
                var msg = string.Join("; ", result.Errors.Select(e => e.Description));
                return Redirect($"/auth/register?error={Uri.EscapeDataString(msg)}");
            }

            await _signInManager.SignInAsync(user, isPersistent: false);
            return Redirect("/");
        }


        [HttpPost("/auth/logout-mvc")]
        public async Task<IActionResult> LogoutMvc()
        {
            await _signInManager.SignOutAsync();
            return Redirect("/");
        }
    }
}
