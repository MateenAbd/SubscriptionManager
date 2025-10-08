using System.Threading;
using System.Threading.Tasks;
using SubscriptionManager.Models.Domain;

namespace SubscriptionManager.Services.Interfaces
{
    public interface INotificationService
    {
        Task HandleAsync(NotificationMessage message, CancellationToken ct = default);
    }
}