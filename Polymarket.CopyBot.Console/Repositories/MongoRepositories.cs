using MongoDB.Driver;
using Polymarket.CopyBot.Console.Configuration;
using Polymarket.CopyBot.Console.Models;

namespace Polymarket.CopyBot.Console.Repositories
{
    public interface IUserActivityRepository
    {
        IMongoCollection<UserActivity> GetCollection(string walletAddress);
    }

    public class UserActivityRepository : IUserActivityRepository
    {
        private readonly IMongoDatabase _database;

        public UserActivityRepository(IMongoDatabase database)
        {
            _database = database;
        }

        public IMongoCollection<UserActivity> GetCollection(string walletAddress)
        {
            var collectionName = $"user_activities_{walletAddress}";
            return _database.GetCollection<UserActivity>(collectionName);
        }
    }

    public interface IUserPositionRepository
    {
        IMongoCollection<UserPosition> GetCollection(string walletAddress);
    }

    public class UserPositionRepository : IUserPositionRepository
    {
        private readonly IMongoDatabase _database;

        public UserPositionRepository(IMongoDatabase database)
        {
            _database = database;
        }

        public IMongoCollection<UserPosition> GetCollection(string walletAddress)
        {
            var collectionName = $"user_positions_{walletAddress}";
            return _database.GetCollection<UserPosition>(collectionName);
        }
    }
}
