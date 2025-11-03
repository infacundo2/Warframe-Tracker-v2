using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

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
        public async Task<IActionResult> LoginMvc(string username, string password, bool rememberMe = false)
        {
            Console.WriteLine("➡️ POST /auth/login-mvc recibido");
            Console.WriteLine($"   Usuario: {username}, RememberMe: {rememberMe}");

            var user = await _userManager.FindByNameAsync(username);
            if (user == null)
            {
                Console.WriteLine("❌ Usuario no encontrado");
                return Redirect("/auth/login?error=Usuario%20no%20encontrado");
            }

            var result = await _signInManager.PasswordSignInAsync(user, password, rememberMe, false);
            if (!result.Succeeded)
            {
                Console.WriteLine("❌ Credenciales inválidas");
                return Redirect("/auth/login?error=Credenciales%20invalidas");
            }

            Console.WriteLine("✅ Inicio de sesion exitoso por MVC");
            return Redirect("/");
        }

        [HttpPost("/auth/logout-mvc")]
        public async Task<IActionResult> LogoutMvc()
        {
            Console.WriteLine("➡️ POST /auth/logout-mvc recibido");
            await _signInManager.SignOutAsync();
            Console.WriteLine("👋 Sesión cerrada desde MVC");
            return Redirect("/");
        }
    }
}
