using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SubscriptionManager.Models.Domain;
using SubscriptionManager.Models.ViewModels;
using SubscriptionManager.Services.Interfaces;

namespace SubscriptionManager.Controllers
{
    [Authorize]
    public class SubscriptionsController : Controller
    {
        private readonly ISubscriptionService _subs;
        private readonly IPlanService _plans;
        private readonly IUserService _users;

        public SubscriptionsController(ISubscriptionService subs, IPlanService plans, IUserService users)
        {
            _subs = subs;
            _plans = plans;
            _users = users;
        }

        private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpGet]
        public async Task<IActionResult> Dashboard(CancellationToken ct)
        {
            var user = await _users.GetByIdAsync(CurrentUserId, ct);
            var subs = await _users.GetUserSubscriptionsAsync(CurrentUserId, ct);
            var pays = await _users.GetUserPaymentsAsync(CurrentUserId, ct);

            var vm = new DashboardViewModel
            {
                CurrentUser = user,
                Subscriptions = subs,
                Payments = pays
            };
            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> Create(CancellationToken ct)
        {
            ViewBag.Plans = await _plans.GetAllAsync(ct);
            return View(new SubscriptionCreateViewModel());
        }

        [HttpGet]
        public async Task<IActionResult> PlanInfo(int id, CancellationToken ct)
        {
            var plan = await _plans.GetByIdAsync(id, ct);
            if (plan == null) return NotFound();
            return Json(new { plan.PlanId, plan.Name, plan.Price, plan.BillingCycle, plan.DurationDays, plan.Features });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SubscriptionCreateViewModel vm, CancellationToken ct)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Plans = await _plans.GetAllAsync(ct);
                return View(vm);
            }

            try
            {
                await _subs.SubscribeAsync(CurrentUserId, vm.PlanId, vm.PaymentMethod, vm.AutoRenew, ct);
                TempData["Toast"] = "Subscription created.";
                return RedirectToAction(nameof(Dashboard));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                ViewBag.Plans = await _plans.GetAllAsync(ct);
                return View(vm);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Renew(int id, CancellationToken ct)
        {
            var sub = await _subs.GetByIdAsync(id, ct);
            if (sub == null || sub.UserId != CurrentUserId) return NotFound();
            return View(sub);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Renew(int id, string paymentMethod, CancellationToken ct)
        {
            try
            {
                var sub = await _subs.GetByIdAsync(id, ct);
                if (sub == null || sub.UserId != CurrentUserId) return NotFound();

                await _subs.RenewAsync(id, paymentMethod, ct);
                TempData["Toast"] = "Subscription renewed.";
                return RedirectToAction(nameof(Dashboard));
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(Dashboard));
            }
        }

        [HttpGet]
        public async Task<IActionResult> Cancel(int id, CancellationToken ct)
        {
            var sub = await _subs.GetByIdAsync(id, ct);
            if (sub == null || sub.UserId != CurrentUserId) return NotFound();
            return View(sub);
        }

        [HttpPost, ActionName("Cancel")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelConfirmed(int id, CancellationToken ct)
        {
            try
            {
                var sub = await _subs.GetByIdAsync(id, ct);
                if (sub == null || sub.UserId != CurrentUserId) return NotFound();

                await _subs.CancelAsync(id, ct);
                TempData["Toast"] = "Subscription cancelled.";
                return RedirectToAction(nameof(Dashboard));
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(Dashboard));
            }
        }
    }
}