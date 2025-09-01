using System;

namespace API.Dtos;

public class MessageRequestDto
{
    public string ReceiverId { get; set; } = null!;
    public string Content { get; set; } = null!;
}