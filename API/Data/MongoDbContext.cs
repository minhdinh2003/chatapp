using API.Models;
using MongoDB.Driver;

namespace API.Data;

public class MongoDbContext
{
    private readonly IMongoDatabase _database;
    public IMongoCollection<Message> Messages { get; }

    public MongoDbContext(IMongoDatabase database)
    {
        _database = database;
        Messages = _database.GetCollection<Message>("Messages");
    }
}