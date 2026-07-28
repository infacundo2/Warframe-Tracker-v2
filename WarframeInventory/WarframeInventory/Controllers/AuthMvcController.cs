using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace WarframeInventory.Controllers;

public class AuthMvcController : Controller
{
    private static readonly Regex ValidUserName =
        new("^[A-Za-z0-9._-]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly UserManager<IdentityUser> _userManager;

    public AuthMvcController(
        SignInManager<IdentityUser> signInManager,
        UserManager<IdentityUser> userManager)
    {
        _signInManager = signInManager;
        _userManager = userManager;
    }

    [HttpPost("/auth/login-mvc")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> LoginMvc(
        string username,
        string password,
        bool rememberMe = false,
        string? returnUrl = null)
    {
        username = username.Trim();
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return RedirectToLogin("Escribe tu usuario y contraseña.", username, returnUrl);

        var user = await _userManager.FindByNameAsync(username);
        if (user is null)
            return RedirectToLogin(
                "El usuario o la contraseña no son correctos.",
                username,
                returnUrl);

        var result = await _signInManager.PasswordSignInAsync(user, password, rememberMe, true);
        if (result.IsLockedOut)
            return RedirectToLogin(
                "La cuenta está bloqueada temporalmente por demasiados intentos. Inténtalo en 15 minutos.",
                username,
                returnUrl);
        if (!result.Succeeded)
            return RedirectToLogin(
                "El usuario o la contraseña no son correctos.",
                username,
                returnUrl);

        return Redirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/");
    }

    [HttpPost("/auth/register-mvc")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> RegisterMvc(
        string username,
        string email,
        string password,
        string confirmPassword,
        string? returnUrl = null)
    {
        username = username.Trim();
        email = email.Trim();

        if (string.IsNullOrWhiteSpace(username))
            return RedirectToRegister("Debes escribir un nombre de usuario.", "", email);
        if (username.Length is < 3 or > 24)
            return RedirectToRegister(
                "El usuario debe tener entre 3 y 24 caracteres.",
                "",
                email);
        if (!ValidUserName.IsMatch(username))
            return RedirectToRegister(
                "El usuario solo puede contener letras sin tilde, números, punto, guion y guion bajo.",
                "",
                email);

        if (string.IsNullOrWhiteSpace(email))
            return RedirectToRegister("Debes escribir un correo electrónico.", username, "");
        if (!new EmailAddressAttribute().IsValid(email))
            return RedirectToRegister("El correo electrónico no tiene un formato válido.", username, "");

        if (string.IsNullOrEmpty(password))
            return RedirectToRegister("Debes escribir una contraseña.", username, email);
        if (password.Length < 10)
            return RedirectToRegister(
                "La contraseña debe tener al menos 10 caracteres.",
                username,
                email);
        if (!password.Any(char.IsLower))
            return RedirectToRegister(
                "La contraseña debe incluir al menos una letra minúscula.",
                username,
                email);
        if (!password.Any(char.IsDigit))
            return RedirectToRegister(
                "La contraseña debe incluir al menos un número.",
                username,
                email);
        if (password != confirmPassword)
            return RedirectToRegister("Las contraseñas no coinciden.", username, email);

        if (await _userManager.FindByNameAsync(username) is not null)
            return RedirectToRegister(
                "Ese nombre de usuario ya está en uso. Elige otro.",
                "",
                email);
        if (await _userManager.FindByEmailAsync(email) is not null)
            return RedirectToRegister(
                "Ese correo electrónico ya está asociado a una cuenta.",
                username,
                "");

        var user = new IdentityUser { UserName = username, Email = email };
        var result = await _userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            var message = string.Join(
                " ",
                result.Errors.Select(TranslateIdentityError).Distinct());
            return RedirectToRegister(message, username, email);
        }

        await _signInManager.SignInAsync(user, isPersistent: false);
        return Redirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/");
    }

    [HttpPost("/auth/logout-mvc")]
    public async Task<IActionResult> LogoutMvc()
    {
        await _signInManager.SignOutAsync();
        return Redirect("/");
    }

    private IActionResult RedirectToRegister(string error, string username, string email)
    {
        var url = "/auth/register"
                  + $"?error={Uri.EscapeDataString(error)}"
                  + $"&usuario={Uri.EscapeDataString(username)}"
                  + $"&correo={Uri.EscapeDataString(email)}";
        return Redirect(url);
    }

    private IActionResult RedirectToLogin(
        string error,
        string username,
        string? returnUrl)
        => Redirect(
            "/auth/login"
            + $"?error={Uri.EscapeDataString(error)}"
            + $"&usuario={Uri.EscapeDataString(username)}"
            + (Url.IsLocalUrl(returnUrl)
                ? $"&returnUrl={Uri.EscapeDataString(returnUrl!)}"
                : ""));

    private static string TranslateIdentityError(IdentityError error) => error.Code switch
    {
        "DuplicateUserName" => "Ese nombre de usuario ya está en uso.",
        "DuplicateEmail" => "Ese correo electrónico ya está asociado a una cuenta.",
        "InvalidUserName" => "El nombre de usuario contiene caracteres no permitidos.",
        "InvalidEmail" => "El correo electrónico no tiene un formato válido.",
        "PasswordTooShort" => "La contraseña es demasiado corta.",
        "PasswordRequiresDigit" => "La contraseña debe incluir al menos un número.",
        "PasswordRequiresLower" => "La contraseña debe incluir al menos una letra minúscula.",
        "PasswordRequiresUpper" => "La contraseña debe incluir al menos una letra mayúscula.",
        "PasswordRequiresNonAlphanumeric" =>
            "La contraseña debe incluir al menos un carácter especial.",
        "PasswordRequiresUniqueChars" =>
            "La contraseña debe incluir más caracteres diferentes.",
        _ => "No se pudo crear la cuenta. Revisa los datos ingresados."
    };
}
