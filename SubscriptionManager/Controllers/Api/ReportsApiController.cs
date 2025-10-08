using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using SubscriptionManager.Models.Domain;
using SubscriptionManager.Services.Interfaces;

namespace SubscriptionManager.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = AppRoles.Admin)]
    public class ReportsApiController : ControllerBase
    {
        private readonly IReportService _reports;
        private readonly IChannelProducer<SubscriptionManager.Models.Domain.LogMessage> _logProducer;

        public ReportsApiController(IReportService reports, IChannelProducer<SubscriptionManager.Models.Domain.LogMessage> logProducer)
        {
            _reports = reports;
            _logProducer = logProducer;
        }

        [HttpGet("revenue")]
        public async Task<IActionResult> Revenue([FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken ct)
        {
            var total = await _reports.GetRevenueAsync(from, to, ct);
            _logProducer.TryWrite(new SubscriptionManager.Models.Domain.LogMessage { Action = "API.ReportRevenue", Message = $"{from:d}-{to:d}" });
            return Ok(new { from, to, totalRevenue = total });
        }

        [HttpGet("plan-metrics")]
        public async Task<IActionResult> PlanMetrics([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
        {
            var data = await _reports.GetPlanMetricsAsync(from, to, ct);
            return Ok(data);
        }

        [HttpGet("churn")]
        public async Task<IActionResult> Churn(CancellationToken ct)
        {
            var rate = await _reports.GetChurnRateAsync(ct);
            return Ok(new { churnRate = rate });
        }
    }
}