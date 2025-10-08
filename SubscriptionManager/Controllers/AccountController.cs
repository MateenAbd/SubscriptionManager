using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SubscriptionManager.Models.Domain;
using SubscriptionManager.Models.ViewModels;
using SubscriptionManager.Services.Interfaces;

namespace SubscriptionManager.Controllers
{
    public class AccountController : Controller
    {
        private readonly IUserService _users;
        private readonly IJwtTokenService _jwt;
        private readonly IChannelProducer<LogMessage> _logProducer;

        public AccountController(IUserService users, IJwtTokenService jwt, IChannelProducer<LogMessage> logProducer)
        {
            _users = users;
            _jwt = jwt;
            _logProducer = logProducer;
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Register() => View(new RegisterViewModel());

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel vm, CancellationToken ct)
        {
            if (!ModelState.IsValid) return View(vm);

            try
            {
                var userId = await _users.RegisterAsync(vm, ct);
                var user = await _users.GetByIdAsync(userId, ct);
                if (user == null) return RedirectToAction("Login");

                await SignInAsync(user);
                _logProducer.TryWrite(new LogMessage { UserId = user.UserId, Action = "UserLogin", Message = "User logged in after registration." });

                // Also issue JWT and store in HttpOnly cookie for convenience
                var token = _jwt.Generate(user);
                Response.Cookies.Append("AppJwt", token, new Microsoft.AspNetCore.Http.CookieOptions { HttpOnly = true, Secure = false, SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax });

                return RedirectToAction("Dashboard", "Subscriptions");
            }
            catch (System.Exception ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return View(vm);
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View(new LoginViewModel());
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel vm, string? returnUrl, CancellationToken ct)
        {
            if (!ModelState.IsValid) return View(vm);

            var user = await _users.GetByEmailAsync(vm.Email, ct);
            if (user == null || !BCrypt.Net.BCrypt.Verify(vm.Password, user.PasswordHash))
            {
                ModelState.AddModelError(string.Empty, "Invalid email or password.");
                return View(vm);
            }

            await SignInAsync(user);
            _logProducer.TryWrite(new LogMessage { UserId = user.UserId, Action = "UserLogin", Message = $"User {user.Email} logged in." });

            // Issue JWT and store in HttpOnly cookie for API usage (Postman can copy from response Set-Cookie if needed)
            var token = _jwt.Generate(user);
            Response.Cookies.Append("AppJwt", token, new Microsoft.AspNetCore.Http.CookieOptions { HttpOnly = true, Secure = false, SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax });

            if (user.Role == AppRoles.Admin)
                return RedirectToAction("Dashboard", "Admin");
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);
            return RedirectToAction("Dashboard", "Subscriptions");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            Response.Cookies.Delete("AppJwt");
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult AccessDenied() => View("AccessDenied");

        // For Postman: exchange credentials for JWT without cookie auth flow
        [HttpPost]
        [AllowAnonymous]
        [Route("Account/ApiLogin")]
        public async Task<IActionResult> ApiLogin([FromBody] LoginViewModel vm, CancellationToken ct)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var user = await _users.GetByEmailAsync(vm.Email, ct);
            if (user == null || !BCrypt.Net.BCrypt.Verify(vm.Password, user.PasswordHash))
                return Unauthorized();

            var token = _jwt.Generate(user);
            return Ok(new { token, user = new { user.UserId, user.Email, user.Role } });
        }

        private async Task SignInAsync(User user)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("uid", user.UserId.ToString())
            };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
        }
    }
}