using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SubscriptionManager.Models.Domain;
using SubscriptionManager.Models.ViewModels;
using SubscriptionManager.Services.Interfaces;

namespace SubscriptionManager.Controllers
{
    [Authorize(Roles = AppRoles.Admin)]
    public class UsersController : Controller
    {
        private readonly IUserService _users;

        public UsersController(IUserService users)
        {
            _users = users;
        }

        [HttpGet]
        public async Task<IActionResult> Index([FromQuery] UserListQuery query, CancellationToken ct)
        {
            var page = await _users.GetPagedAsync(query, ct);
            return View(page);
        }

        [HttpGet]
        public IActionResult Create() => View(new UserFormViewModel());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(UserFormViewModel vm, CancellationToken ct)
        {
            if (!ModelState.IsValid) return View(vm);
            try
            {
                await _users.CreateAsync(vm, ct);
                TempData["Toast"] = "User created.";
                return RedirectToAction(nameof(Index));
            }
            catch (System.Exception ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return View(vm);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id, CancellationToken ct)
        {
            var u = await _users.GetByIdAsync(id, ct);
            if (u == null) return NotFound();

            var vm = new UserFormViewModel
            {
                UserId = u.UserId,
                FirstName = u.FirstName,
                LastName = u.LastName,
                Email = u.Email,
                Phone = u.Phone,
                Address = u.Address,
                Role = u.Role
            };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, UserFormViewModel vm, CancellationToken ct)
        {
            if (!ModelState.IsValid) return View(vm);

            try
            {
                await _users.UpdateAsync(id, vm, ct);
                TempData["Toast"] = "User updated.";
                return RedirectToAction(nameof(Index));
            }
            catch (System.Exception ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return View(vm);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id, CancellationToken ct)
        {
            var user = await _users.GetByIdAsync(id, ct);
            if (user == null) return NotFound();

            var subs = await _users.GetUserSubscriptionsAsync(id, ct);
            var pays = await _users.GetUserPaymentsAsync(id, ct);

            ViewBag.Subscriptions = subs;
            ViewBag.Payments = pays;

            return View(user);
        }

        [HttpGet]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            var user = await _users.GetByIdAsync(id, ct);
            if (user == null) return NotFound();
            return View(user);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id, CancellationToken ct)
        {
            try
            {
                var actorId = int.Parse(User.FindFirst("uid")!.Value);
                await _users.DeleteAsync(id, actorId, ct);
                TempData["Toast"] = "User deleted.";
                return RedirectToAction(nameof(Index));
            }
            catch (System.Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }
    }
}