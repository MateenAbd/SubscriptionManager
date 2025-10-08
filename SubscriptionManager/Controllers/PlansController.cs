using System.Linq;
using System.Text;
using System.Text.Json;
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
    public class PlansController : Controller
    {
        private readonly IPlanService _plans;

        public PlansController(IPlanService plans)
        {
            _plans = plans;
        }

        // Admin list with pagination/search/sort
        [HttpGet]
        public async Task<IActionResult> Index([FromQuery] PlanListQuery query, CancellationToken ct)
        {
            var page = await _plans.GetPagedAsync(query, ct);
            return View(page);
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> Browse([FromQuery] PlanListQuery query, CancellationToken ct)
        {
            if (User?.Identity?.IsAuthenticated == true && User.IsInRole(AppRoles.Admin))
            {
                TempData["Error"] = "Admins cannot browse plans.";
                return RedirectToAction("Dashboard", "Admin");
            }
            query.PageSize = query.PageSize <= 0 ? 12 : query.PageSize;
            var page = await _plans.GetPagedAsync(query, ct);
            return View(page);
        }

        [HttpGet]
        public IActionResult Create() => View(new PlanFormViewModel());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PlanFormViewModel vm, CancellationToken ct)
        {
            if (!ModelState.IsValid) return View(vm);
            try
            {
                var actorId = int.TryParse(User.FindFirst("uid")?.Value, out var id) ? id : (int?)null;
                await _plans.CreateAsync(vm, actorId, ct);
                TempData["Toast"] = "Plan created.";
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
            var plan = await _plans.GetByIdAsync(id, ct);
            if (plan == null) return NotFound();
            var vm = new PlanFormViewModel
            {
                PlanId = plan.PlanId,
                Name = plan.Name,
                Description = plan.Description,
                Price = plan.Price,
                BillingCycle = plan.BillingCycle,
                DurationDays = plan.DurationDays,
                Features = plan.Features
            };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, PlanFormViewModel vm, CancellationToken ct)
        {
            if (!ModelState.IsValid) return View(vm);
            try
            {
                var actorId = int.TryParse(User.FindFirst("uid")?.Value, out var uid) ? uid : (int?)null;
                await _plans.UpdateAsync(id, vm, actorId, ct);
                TempData["Toast"] = "Plan updated.";
                return RedirectToAction(nameof(Index));
            }
            catch (System.Exception ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return View(vm);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            var plan = await _plans.GetByIdAsync(id, ct);
            if (plan == null) return NotFound();
            return View(plan);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id, CancellationToken ct)
        {
            try
            {
                var actorId = int.TryParse(User.FindFirst("uid")?.Value, out var uid) ? uid : (int?)null;
                await _plans.DeleteAsync(id, actorId, ct);
                TempData["Toast"] = "Plan deleted.";
                return RedirectToAction(nameof(Index));
            }
            catch (System.Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpGet]
        public async Task<IActionResult> ExportJson([FromQuery] PlanListQuery query, CancellationToken ct)
        {
            query.PageSize = 1000;
            var page = await _plans.GetPagedAsync(query, ct);
            var json = JsonSerializer.Serialize(page.Items, new JsonSerializerOptions { WriteIndented = true });
            var bytes = Encoding.UTF8.GetBytes(json);
            return File(bytes, "application/json", "plans.json");
        }

        [HttpGet]
        public async Task<IActionResult> ExportCsv([FromQuery] PlanListQuery query, CancellationToken ct)
        {
            query.PageSize = 1000;
            var page = await _plans.GetPagedAsync(query, ct);

            var sb = new StringBuilder();
            sb.AppendLine("PlanId,Name,Price,BillingCycle,DurationDays,CreatedAt");
            foreach (var p in page.Items)
            {
                sb.AppendLine($"{p.PlanId},\"{p.Name.Replace("\"", "\"\"")}\",{p.Price},{p.BillingCycle},{p.DurationDays},{p.CreatedAt:yyyy-MM-dd}");
            }
            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            return File(bytes, "text/csv", "plans.csv");
        }
    }
}