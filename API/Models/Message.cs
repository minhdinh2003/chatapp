using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace API.Models;

public class Message
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;
    
    public string Content { get; set; } = null!;
    
    public string SenderId { get; set; } = null!;
    
    public string ReceiverId { get; set; } = null!;
    
    public bool IsRead { get; set; }
    
    public DateTime CreatedDate { get; set; }
    
    [BsonIgnore]
    public AppUser Sender { get; set; } = null!;
    
    [BsonIgnore]
    public AppUser Receiver { get; set; } = null!;
}