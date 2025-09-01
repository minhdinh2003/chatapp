// Dtos/MessageResponseDto.cs
namespace API.Dtos
{
    public class MessageResponseDto
    {
        public string Id { get; set; } = null!;
        public string SenderId { get; set; } = null!;
        public string ReceiverId { get; set; } = null!;
        public string Content { get; set; } = null!;
        public bool IsRead { get; set; }
        public DateTime CreatedDate { get; set; }
        public string MessageType { get; set; } = "Text"; // Thêm MessageType
    }
}