using API.Common;
using API.Dtos;
using API.Extentions;
using API.Models;
using API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace API.Endpoints;

public static class AccountEndpoint
{
    public static RouteGroupBuilder MapAccountEndpoint(this WebApplication app)
    {
        var group = app.MapGroup("/api/account").WithTags("account");

        // Register endpoint with role assignment
        group.MapPost("/register", async (HttpContext context,
            UserManager<AppUser> userManager,
            RoleManager<IdentityRole> roleManager,
            [FromForm] string fullName,
            [FromForm] string email,
            [FromForm] string username,
            [FromForm] string password,
            [FromForm] IFormFile? profileImage,
            [FromForm] string role = "User") =>
        {
            var userFromDb = await userManager.FindByEmailAsync(email);
            if (userFromDb is not null)
            {
                return Results.BadRequest(Response<string>.Failure("User already exists."));
            }

            if (profileImage is null)
            {
                return Results.BadRequest(Response<string>.Failure("Profile image is required."));
            }

            try
            {
                var picture = await FileUpload.Upload(profileImage);
                var pictureUrl = $"{context.Request.Scheme}://{context.Request.Host}/Uploads/{picture}";

                var user = new AppUser
                {
                    Email = email,
                    FullName = fullName,
                    UserName = username,
                    ProfileImage = pictureUrl
                };
                var result = await userManager.CreateAsync(user, password);
                if (!result.Succeeded)
                {
                    return Results.BadRequest(Response<string>.Failure(result
                        .Errors.Select(x => x.Description).FirstOrDefault()!));
                }

                // Ensure roles exist
                if (!await roleManager.RoleExistsAsync("Admin"))
                {
                    await roleManager.CreateAsync(new IdentityRole("Admin"));
                }
                if (!await roleManager.RoleExistsAsync("User"))
                {
                    await roleManager.CreateAsync(new IdentityRole("User"));
                }

                // Assign role (default to User if invalid role provided)
                var roleToAssign = role == "Admin" && context.User.IsInRole("Admin") ? "Admin" : "User";
                await userManager.AddToRoleAsync(user, roleToAssign);

                return Results.Ok(Response<string>.Success(pictureUrl, "User created successfully."));
            }
            catch (Exception ex)
            {
                return Results.BadRequest(Response<string>.Failure($"Failed to upload profile image: {ex.Message}"));
            }
        }).DisableAntiforgery();

        // Login endpoint
        group.MapPost("/login", async (UserManager<AppUser> userManager, TokenService tokenService, LoginDto dto) =>
        {
            if (dto is null)
            {
                return Results.BadRequest(Response<string>.Failure("Invalid login details"));
            }

            var user = await userManager.FindByEmailAsync(dto.Email);

            if (user is null)
            {
                return Results.BadRequest(Response<string>.Failure("User not found"));
            }

            var result = await userManager.CheckPasswordAsync(user, dto.Password);

            if (!result)
            {
                return Results.BadRequest(Response<string>.Failure("Invalid password"));
            }

            var token = await tokenService.GenerateToken(user.Id, user.UserName!);

            return Results.Ok(Response<string>.Success(token, "Login Successfully"));
        });

        // Get current user
        group.MapGet("/me", async (HttpContext context, UserManager<AppUser> userManager) =>
        {
            var currentLoggedInUserId = context.User.GetUserId();
            var currentLoggedInUser = await userManager.Users.SingleOrDefaultAsync(x => x.Id == currentLoggedInUserId.ToString());
            if (currentLoggedInUser == null)
            {
                return Results.NotFound(Response<string>.Failure("User not found"));
            }

            var roles = await userManager.GetRolesAsync(currentLoggedInUser); // Lấy roles

            var userDto = new OnlineUserDto
            {
                Id = currentLoggedInUser.Id,
                UserName = currentLoggedInUser.UserName,
                FullName = currentLoggedInUser.FullName,
                Email = currentLoggedInUser.Email,
                ProfileImage = currentLoggedInUser.ProfileImage,
                IsOnline = false, // Modify based on your chat system
                Roles = roles
            };

            return Results.Ok(Response<OnlineUserDto>.Success(userDto, "User fetched successfully."));
        }).RequireAuthorization();
        // Admin-only endpoints for user management
        group.MapGet("/users", async (UserManager<AppUser> userManager) =>
        {
            var users = await userManager.Users.Select(u => new OnlineUserDto
            {
                Id = u.Id,
                UserName = u.UserName,
                FullName = u.FullName,
                Email = u.Email,
                ProfileImage = u.ProfileImage,
                IsOnline = false // Modify based on your chat system
            }).ToListAsync();

            return Results.Ok(Response<OnlineUserDto[]>.Success(users.ToArray(), "Users fetched successfully."));
        }).RequireAuthorization("AdminOnly");

        group.MapGet("/users/{id}", async (string id, UserManager<AppUser> userManager) =>
        {
            var user = await userManager.FindByIdAsync(id);
            if (user == null)
            {
                return Results.NotFound(Response<string>.Failure("User not found"));
            }
            var roles = await userManager.GetRolesAsync(user);
            var userDto = new OnlineUserDto
            {
                Id = user.Id,
                UserName = user.UserName,
                FullName = user.FullName,
                Email = user.Email,
                ProfileImage = user.ProfileImage,
                Roles = roles,
                IsOnline = false // Modify based on your chat system
            };

            return Results.Ok(Response<OnlineUserDto>.Success(userDto, "User fetched successfully."));
        }).RequireAuthorization("AdminOnly");

        group.MapPut("/users/{id}", async (string id, UserManager<AppUser> userManager, [FromForm] string fullName, [FromForm] string email, [FromForm] IFormFile? profileImage, HttpContext context) =>
        {
            var user = await userManager.FindByIdAsync(id);
            if (user == null)
            {
                return Results.NotFound(Response<string>.Failure("User not found"));
            }

            user.FullName = fullName;
            user.Email = email;

            if (profileImage != null)
            {
                try
                {
                    var picture = await FileUpload.Upload(profileImage);
                    user.ProfileImage = $"{context.Request.Scheme}://{context.Request.Host}/Uploads/{picture}";
                }
                catch (Exception ex)
                {
                    return Results.BadRequest(Response<string>.Failure($"Failed to upload profile image: {ex.Message}"));
                }
            }

            var result = await userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                return Results.BadRequest(Response<string>.Failure(result.Errors.Select(x => x.Description).FirstOrDefault()!));
            }

            return Results.Ok(Response<string>.Success("", "User updated successfully."));
        }).RequireAuthorization("AdminOnly").DisableAntiforgery();

        group.MapDelete("/users/{id}", async (string id, UserManager<AppUser> userManager) =>
        {
            var user = await userManager.FindByIdAsync(id);
            if (user == null)
            {
                return Results.NotFound(Response<string>.Failure("User not found"));
            }

            var result = await userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                return Results.BadRequest(Response<string>.Failure(result.Errors.Select(x => x.Description).FirstOrDefault()!));
            }

            return Results.Ok(Response<string>.Success("", "User deleted successfully."));
        }).RequireAuthorization("AdminOnly");

        // Update user roles endpoint (modified to accept comma-separated roles)
        group.MapPut("/users/{id}/roles", async (string id, UserManager<AppUser> userManager, RoleManager<IdentityRole> roleManager, [FromForm] string newRoles, HttpContext context) =>
        {
            if (!context.User.IsInRole("Admin"))
            {
                return Results.Forbid();
            }

            var user = await userManager.FindByIdAsync(id);
            if (user == null)
            {
                return Results.NotFound(Response<string>.Failure("User not found"));
            }

            // Split comma-separated roles and trim whitespace
            var roles = newRoles.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(r => r.Trim()).ToArray();
            if (roles.Length == 0)
            {
                return Results.BadRequest(Response<string>.Failure("At least one role must be specified."));
            }

            // Validate roles
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    return Results.BadRequest(Response<string>.Failure($"Invalid role specified: {role}"));
                }
            }

            // Get current roles
            var currentRoles = await userManager.GetRolesAsync(user);

            // Remove all existing roles
            var removeResult = await userManager.RemoveFromRolesAsync(user, currentRoles);
            if (!removeResult.Succeeded)
            {
                return Results.BadRequest(Response<string>.Failure("Failed to update roles: " + removeResult.Errors.FirstOrDefault()?.Description));
            }

            // Add new roles
            foreach (var role in roles)
            {
                var addResult = await userManager.AddToRoleAsync(user, role);
                if (!addResult.Succeeded)
                {
                    return Results.BadRequest(Response<string>.Failure($"Failed to add role {role}: " + addResult.Errors.FirstOrDefault()?.Description));
                }
            }

            return Results.Ok(Response<string>.Success("", "User roles updated successfully."));
        }).RequireAuthorization("AdminOnly").DisableAntiforgery();

        return group;
    }
}