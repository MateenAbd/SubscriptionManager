using SubscriptionManager.Models.Domain;

namespace SubscriptionManager.Services.Interfaces
{
    public interface IJwtTokenService
    {
        string Generate(User user);
    }
}