using System;
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
    public class ReportsController : Controller
    {
        private readonly IReportService _reports;
        private readonly IChannelProducer<LogMessage> _logProducer;

        public ReportsController(IReportService reports, IChannelProducer<LogMessage> logProducer)
        {
            _reports = reports;
            _logProducer = logProducer;
        }

        [HttpGet]
        public IActionResult Index() => View();

        [HttpGet]
        public async Task<IActionResult> Revenue([FromQuery] ReportQueryViewModel q, CancellationToken ct)
        {
            if (q.From == default || q.To == default)
            {
                q.From = DateTime.UtcNow.AddDays(-30);
                q.To = DateTime.UtcNow;
            }

            var total = await _reports.GetRevenueAsync(q.From, q.To, ct);
            _logProducer.TryWrite(new LogMessage { Action = "ReportRevenue", Message = $"Revenue report {q.From:d} - {q.To:d}" });
            ViewBag.Query = q;
            ViewBag.Total = total;
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> PlanMetrics(DateTime? from, DateTime? to, CancellationToken ct)
        {
            var data = await _reports.GetPlanMetricsAsync(from, to, ct);
            _logProducer.TryWrite(new LogMessage { Action = "ReportPlanMetrics", Message = "Plan metrics generated." });
            ViewBag.From = from;
            ViewBag.To = to;
            return View(data);
        }

        [HttpGet]
        public async Task<IActionResult> Churn(CancellationToken ct)
        {
            var rate = await _reports.GetChurnRateAsync(ct);
            _logProducer.TryWrite(new LogMessage { Action = "ReportChurn", Message = "Churn report generated." });
            ViewBag.Rate = rate;
            return View();
        }
    }
}