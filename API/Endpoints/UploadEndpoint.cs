// UploadEndpoint.cs
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using API.Common; // Chứa FileUpload

public static class UploadEndpoint
{
    public static RouteGroupBuilder MapUploadEndpoint(this WebApplication app)
    {
        var group = app.MapGroup("/api/upload").WithTags("upload");

        // UploadEndpoint.cs
        group.MapPost("/", async (HttpContext context, [FromForm] IFormFile file) =>
        {
            if (file == null || file.Length == 0)
            {
                return Results.BadRequest(new { message = "No file uploaded." });
            }

            if (!file.ContentType.StartsWith("image/"))
            {
                return Results.BadRequest(new { message = "Only image files are allowed." });
            }
            if (file.Length > 5 * 1024 * 1024)
            {
                return Results.BadRequest(new { message = "File size exceeds 5MB." });
            }

            try
            {
                var picture = await FileUpload.Upload(file);
                var pictureUrl = $"{context.Request.Scheme}://{context.Request.Host}/Uploads/{picture}";
                return Results.Ok(new { url = pictureUrl });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { message = $"Failed to upload image: {ex.Message}" });
            }
        }).RequireAuthorization().DisableAntiforgery();
        return group;
    }
}