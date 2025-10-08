using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SubscriptionManager.Models.Domain;
using SubscriptionManager.Services.Interfaces;

namespace SubscriptionManager.Controllers
{
    [Authorize(Roles = AppRoles.Admin)]
    public class AdminController : Controller
    {
        private readonly IReportService _reports;

        public AdminController(IReportService reports)
        {
            _reports = reports;
        }

        [HttpGet]
        public async Task<IActionResult> Dashboard(CancellationToken ct)
        {
            var to = DateTime.UtcNow;
            var from = to.AddDays(-30);
            var revenue = await _reports.GetRevenueAsync(from, to, ct);
            var churn = await _reports.GetChurnRateAsync(ct);
            var avgDuration = await _reports.GetAverageSubscriptionDurationDaysAsync(ct);

            ViewBag.From = from;
            ViewBag.To = to;
            ViewBag.Revenue = revenue;
            ViewBag.Churn = churn;
            ViewBag.AvgDuration = avgDuration;

            return View();
        }
    }
}