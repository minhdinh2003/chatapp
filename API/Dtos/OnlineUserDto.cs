using System;

namespace API.Dtos
{
    public class OnlineUserDto
    {
        public string? Id { get; set; }
        public string? ConnectionId { get; set; }
        public string? UserName { get; set; }
        public string? Email { get; set; }
        public string? FullName { get; set; }
        public string? ProfileImage { get; set; }
        public bool IsOnline { get; set; }
        public int UnreadCount { get; set; }
        public IList<string> Roles { get; set; } = [];
    }
}