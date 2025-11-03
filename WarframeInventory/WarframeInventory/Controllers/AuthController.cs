using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;

namespace WarframeInventory.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : Controller
    {
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly UserManager<IdentityUser> _userManager;

        public AuthController(SignInManager<IdentityUser> signInManager, UserManager<IdentityUser> userManager)
        {
            _signInManager = signInManager;
            _userManager = userManager;
        }

        // ============================================================
        // POST: /api/auth/login
        // ============================================================
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"➡️ POST /api/auth/login recibido");
            Console.ResetColor();
            Console.WriteLine($"   Usuario: {dto.Username}, RememberMe: {dto.RememberMe}");

            var user = await _userManager.FindByNameAsync(dto.Username);
            if (user == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("❌ Usuario no encontrado");
                Console.ResetColor();
                return Unauthorized("Usuario no encontrado.");
            }

            Console.WriteLine("🔑 Usuario encontrado, validando contraseña...");
            var result = await _signInManager.PasswordSignInAsync(user, dto.Password, dto.RememberMe, false);

            if (result.Succeeded)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("✅ Contraseña válida, iniciando sesión y creando cookie...");
                Console.ResetColor();

                await HttpContext.SignInAsync(
                    IdentityConstants.ApplicationScheme,
                    new ClaimsPrincipal(await _signInManager.CreateUserPrincipalAsync(user)),
                    new AuthenticationProperties { IsPersistent = dto.RememberMe });

                var cookies = HttpContext.Response.Headers["Set-Cookie"].ToArray();
                Console.WriteLine($"🍪 Cookies enviadas ({cookies.Length}):");
                foreach (var c in cookies)
                    Console.WriteLine($"   -> {c}");

                return Ok(new { message = "Login exitoso", user = user.UserName });
            }

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("❌ Credenciales inválidas");
            Console.ResetColor();
            return Unauthorized("Credenciales inválidas.");
        }

        // ============================================================
        // POST: /api/auth/register
        // ============================================================
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            Console.WriteLine("➡️ POST /api/auth/register recibido");
            Console.WriteLine($"   Usuario: {dto.Username}, Email: {dto.Email}");

            if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Password))
                return BadRequest("Usuario o contraseña vacíos.");

            var existing = await _userManager.FindByNameAsync(dto.Username);
            if (existing != null)
            {
                Console.WriteLine("❌ Usuario ya existe");
                return BadRequest("Ese usuario ya existe.");
            }

            var user = new IdentityUser { UserName = dto.Username, Email = dto.Email };
            var result = await _userManager.CreateAsync(user, dto.Password);

            if (!result.Succeeded)
            {
                var msg = string.Join("; ", result.Errors.Select(e => e.Description));
                Console.WriteLine($"❌ Error creando usuario: {msg}");
                return BadRequest(msg);
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✅ Usuario creado correctamente, iniciando sesión...");
            Console.ResetColor();

            await _signInManager.SignInAsync(user, isPersistent: false);
            Console.WriteLine("🍪 Cookie creada automáticamente en registro");

            return Ok(new { message = "Usuario creado e ingresado", user = user.UserName });
        }

        // ============================================================
        // POST: /api/auth/logout
        // ============================================================
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            Console.WriteLine("➡️ POST /api/auth/logout recibido");
            await _signInManager.SignOutAsync();
            Console.WriteLine("👋 Sesión cerrada y cookie eliminada");
            return Ok(new { message = "Sesión cerrada" });
        }

        [HttpGet("me")]
        public IActionResult Me()
        {
            if (User?.Identity?.IsAuthenticated == true)
            {
                Console.WriteLine($"👤 Usuario autenticado: {User.Identity.Name}");
                return Ok(new { user = User.Identity.Name });
            }

            Console.WriteLine("🚫 Usuario no autenticado.");
            return Unauthorized("No autenticado.");
        }
    }

    // ============================================================
    // DTOs
    // ============================================================
    public class LoginDto
    {
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public bool RememberMe { get; set; } = false;
    }

    public class RegisterDto
    {
        public string Username { get; set; } = "";
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
    }
}
