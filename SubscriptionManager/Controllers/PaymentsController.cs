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
    public class PaymentsController : Controller
    {
        private readonly IPaymentService _payments;

        public PaymentsController(IPaymentService payments)
        {
            _payments = payments;
        }

        [HttpGet]
        public async Task<IActionResult> Index([FromQuery] PaymentFilterViewModel filter, CancellationToken ct)
        {
            var page = await _payments.GetPagedAsync(filter, ct);
            ViewBag.Total = await _payments.GetTotalAsync(filter.From, filter.To, filter.Status, ct);
            return View(page);
        }

        [HttpGet]
        public IActionResult Refund(int id)
        {
            ViewBag.PaymentId = id;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RefundConfirmed(int paymentId, CancellationToken ct)
        {
            try
            {
                await _payments.RefundAsync(paymentId, ct);
                TempData["Toast"] = "Payment refunded.";
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