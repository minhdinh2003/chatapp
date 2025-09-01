using System;
using System.Collections.Concurrent;
using API.Data;
using API.Dtos;
using API.Extentions;
using API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using MongoDB.Driver;
using MongoDB.Bson;
using Microsoft.EntityFrameworkCore;

namespace API.Hubs;

[Authorize]
public class ChatHub(UserManager<AppUser> userManager, AppDbContext context, MongoDbContext mongoContext) : Hub
{
    public static readonly ConcurrentDictionary<string, OnlineUserDto> onlineUsers = new();

    public override async Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext();
        var receiverId = httpContext?.Request.Query["senderId"].ToString();
        var userName = Context.User!.Identity!.Name!;
        var currentUser = await userManager.FindByNameAsync(userName);
        var connectionId = Context.ConnectionId;

        if (onlineUsers.ContainsKey(userName))
        {
            onlineUsers[userName].ConnectionId = connectionId;
        }
        else
        {
            var user = new OnlineUserDto
            {
                ConnectionId = connectionId,
                UserName = userName,
                ProfileImage = currentUser!.ProfileImage,
                FullName = currentUser!.FullName
            };
            onlineUsers.TryAdd(userName, user);

            await Clients.AllExcept(connectionId).SendAsync("Notify", currentUser);
        }
        if (!string.IsNullOrEmpty(receiverId))
        {
            await LoadMessages(receiverId);
        }
        await Clients.All.SendAsync("OnlineUsers", await GetAllUsers());
    }
    public async Task SendMessage(MessageRequestDto message)
    {
        if (message == null || string.IsNullOrEmpty(message.ReceiverId) || string.IsNullOrEmpty(message.Content))
        {
            Console.WriteLine("Invalid message data: " + (message == null ? "Message is null" : "ReceiverId or Content is null"));
            return;
        }

        var senderId = Context.User!.Identity!.Name;
        var recipientId = message.ReceiverId;

        var sender = await userManager.FindByNameAsync(senderId!);
        var receiver = await userManager.FindByIdAsync(recipientId);

        if (sender == null || receiver == null)
        {
            Console.WriteLine($"User not found: SenderId={senderId}, ReceiverId={recipientId}");
            return;
        }

        var messageType = message.Content.StartsWith("http") ? "Image" : "Text";

        var newMsg = new Message
        {
            SenderId = sender.Id,
            ReceiverId = receiver.Id,
            IsRead = false,
            CreatedDate = DateTime.UtcNow,
            Content = message.Content,
            MessageType = messageType
        };

        await mongoContext.Messages.InsertOneAsync(newMsg);

        var responseDto = new MessageResponseDto
        {
            Id = newMsg.Id,
            Content = newMsg.Content,
            CreatedDate = newMsg.CreatedDate,
            SenderId = newMsg.SenderId,
            ReceiverId = newMsg.ReceiverId,
            IsRead = newMsg.IsRead,
            MessageType = newMsg.MessageType
        };

        await Clients.User(recipientId).SendAsync("ReceiveNewMessage", responseDto);
        await Clients.User(sender.Id).SendAsync("ReceiveNewMessage", responseDto);
    }

    public async Task LoadMessages(string recipientId, int pageNumber = 1)
    {
        int pageSize = 10;
        var username = Context.User!.Identity!.Name;
        var currentUser = await userManager.FindByNameAsync(username!);

        if (currentUser is null)
        {
            return;
        }

        var filter = Builders<Message>.Filter.Where(x =>
            (x.ReceiverId == currentUser.Id && x.SenderId == recipientId) ||
            (x.SenderId == currentUser.Id && x.ReceiverId == recipientId));

        var messages = await mongoContext.Messages.Find(filter)
            .SortByDescending(x => x.CreatedDate)
            .Skip((pageNumber - 1) * pageSize)
            .Limit(pageSize)
            .Project(x => new MessageResponseDto
            {
                Id = x.Id,
                Content = x.Content,
                CreatedDate = x.CreatedDate,
                ReceiverId = x.ReceiverId,
                SenderId = x.SenderId,
                IsRead = x.IsRead,
                MessageType = x.MessageType
            })
            .ToListAsync();
        messages.Reverse();
        var unreadMessageIds = messages
            .Where(m => m.ReceiverId == currentUser.Id && !m.IsRead)
            .Select(m => m.Id)
            .ToList();

        if (unreadMessageIds.Any())
        {
            var updateFilter = Builders<Message>.Filter.In(x => x.Id, unreadMessageIds);
            var update = Builders<Message>.Update.Set(x => x.IsRead, true);
            await mongoContext.Messages.UpdateManyAsync(updateFilter, update);

            foreach (var m in messages.Where(m => unreadMessageIds.Contains(m.Id)))
            {
                m.IsRead = true;
            }

            var senderIds = messages
                .Where(m => unreadMessageIds.Contains(m.Id))
                .Select(m => m.SenderId)
                .Distinct();

            foreach (var senderId in senderIds)
            {
                await Clients.User(senderId)
                    .SendAsync("MessagesSeen", currentUser.Id, unreadMessageIds);
            }
        }

        await Clients.User(currentUser.Id)
            .SendAsync("ReceiveMessageList", recipientId, messages);
    }
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var username = Context.User!.Identity!.Name;
        onlineUsers.TryRemove(username!, out _);
        await Clients.All.SendAsync("OnlineUsers", await GetAllUsers());
    }

    public async Task NotifyTyping(string recipientUserName)
    {
        var senderUserName = Context.User!.Identity!.Name;

        if (senderUserName is null)
        {
            return;
        }

        var connectionId = onlineUsers.Values.FirstOrDefault(x => x.UserName == recipientUserName)?.ConnectionId;

        if (connectionId != null)
        {
            await Clients.Client(connectionId).SendAsync("NotifyTypingToUser", senderUserName);
        }
    }

    private async Task<IEnumerable<OnlineUserDto>> GetAllUsers()
    {
        var username = Context.User!.GetUserName();

        var onlineUsersSet = new HashSet<string>(onlineUsers.Keys);

        var userList = await userManager.Users.ToListAsync();

        var users = new List<OnlineUserDto>();
        foreach (var u in userList)
        {
            var unreadCount = (int)await mongoContext.Messages.CountDocumentsAsync(x =>
                x.ReceiverId == username && x.SenderId == u.Id && !x.IsRead);

            users.Add(new OnlineUserDto
            {
                Id = u.Id,
                UserName = u.UserName,
                FullName = u.FullName,
                ProfileImage = u.ProfileImage,
                IsOnline = onlineUsersSet.Contains(u.UserName!),
                UnreadCount = unreadCount
            });
        }

        users = users.OrderByDescending(u => u.IsOnline).ToList();

        return users;
    }
}