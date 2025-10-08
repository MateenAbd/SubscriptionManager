using System.Data;

namespace SubscriptionManager.Data
{
    public interface IDbConnectionFactory
    {
        IDbConnection CreateConnection();
    }
}