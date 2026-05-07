using System.Security.Claims;
using FYP_AutomationSystem.Data;
using FYP_AutomationSystem.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace FYP_AutomationSystem.Services
{
    /// <summary>
    /// Server-side custom authentication state provider backed by AuthService.CurrentUser.
    /// Note: For a single-user demo / lab project this is sufficient. For multi-user
    /// production, replace with a per-circuit / cookie-based session store.
    /// </summary>
    public class CustomAuthStateProvider : AuthenticationStateProvider
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly AppDbContext _context;

        public CustomAuthStateProvider(IHttpContextAccessor httpContextAccessor, AppDbContext context)
        {
            _httpContextAccessor = httpContextAccessor;
            _context = context;
        }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var user = AuthService.CurrentUser;
            var httpUser = _httpContextAccessor.HttpContext?.User;

            if (httpUser?.Identity?.IsAuthenticated == true)
            {
                var idValue = httpUser.FindFirstValue(ClaimTypes.NameIdentifier);
                if (int.TryParse(idValue, out var userId))
                {
                    user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);
                    AuthService.CurrentUser = user;
                }
            }

            if (user == null)
            {
                return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role.ToString())
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            return new AuthenticationState(new ClaimsPrincipal(identity));
        }

        public void NotifyUserAuthentication(User user)
        {
            AuthService.CurrentUser = user;
            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        }

        public void NotifyUserLogout()
        {
            AuthService.CurrentUser = null;
            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        }
    }
}
