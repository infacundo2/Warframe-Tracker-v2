using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WarframeInventory.Services
{
    public class RevalidatingIdentityAuthenticationStateProvider<TUser>
        : RevalidatingServerAuthenticationStateProvider where TUser : class
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IdentityOptions _options;

        public RevalidatingIdentityAuthenticationStateProvider(
            ILoggerFactory loggerFactory,
            IServiceScopeFactory scopeFactory,
            IOptions<IdentityOptions> optionsAccessor)
            : base(loggerFactory)
        {
            _scopeFactory = scopeFactory;
            _options = optionsAccessor.Value;
        }

        protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(30);

        protected override async Task<bool> ValidateAuthenticationStateAsync(
            AuthenticationState authenticationState,
            CancellationToken cancellationToken)
        {
            var user = authenticationState.User;
            if (!user.Identity?.IsAuthenticated ?? true)
            {
                Console.WriteLine("🚫 Estado no autenticado, invalida sesión.");
                return false;
            }

            using var scope = _scopeFactory.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TUser>>();
            var userId = userManager.GetUserId(user);
            var storedUser = await userManager.FindByIdAsync(userId);

            if (storedUser != null)
            {
                var username = await userManager.GetUserNameAsync(storedUser);
                Console.WriteLine($"✅ Usuario válido en revalidación: {username}");
                return true;
            }

            Console.WriteLine("⚠️ Usuario eliminado o inválido en BD, cerrando sesión automáticamente.");
            return false;
        }
    }
}
