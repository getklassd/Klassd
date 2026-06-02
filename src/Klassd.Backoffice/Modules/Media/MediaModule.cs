using Klassd.Abstractions.Media;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Klassd.Backoffice.Modules.Media;

/// <summary>Headless media endpoints: list sections/items, upload, stream, update metadata, delete.</summary>
public class MediaModule : IModule
{
    public void RegisterServices(IServiceCollection services, IConfiguration configuration) { }

    public void MapEndpoints(IEndpointRouteBuilder routes)
    {
        var api = routes.MapGroup("/api/media").RequireAuthorization();

        api.MapGet("/sections", (MediaService media) => Results.Ok(media.Sections));

        api.MapGet("/sections/{section}", async (string section, MediaService media) =>
            Results.Ok(await media.ListAsync(section)));

        // Streamed upload (multipart). Body-size limit relaxed for large files (video).
        api.MapPost("/{section}", async (string section, HttpRequest request, MediaService media) =>
        {
            if (!request.HasFormContentType)
                return Results.BadRequest("Expected multipart/form-data with a 'file'.");

            var form = await request.ReadFormAsync();
            var file = form.Files["file"];
            if (file is null || file.Length == 0)
                return Results.BadRequest("Missing 'file'.");

            try
            {
                await using var stream = file.OpenReadStream();
                var record = await media.UploadAsync(section, file.FileName, file.ContentType, file.Length, stream);
                return Results.Created(MediaService.UrlFor(record.Id), record);
            }
            catch (InvalidOperationException ex) { return Results.BadRequest(ex.Message); }
        })
        .DisableAntiforgery()
        .WithMetadata(new DisableRequestSizeLimitAttribute())                                  // allow large/video uploads
        .WithMetadata(new RequestFormLimitsAttribute { MultipartBodyLengthLimit = long.MaxValue, ValueLengthLimit = int.MaxValue });

        // Public delivery of the binary (anonymous + CORS); upload/update/delete stay cookie-protected.
        api.MapGet("/{id}", async Task<Results<FileStreamHttpResult, NotFound>> (string id, MediaService media) =>
        {
            var result = await media.OpenAsync(id);
            if (result is null) return TypedResults.NotFound();
            var (record, stream) = result.Value;
            return TypedResults.Stream(stream, record.ContentType, record.FileName);
        }).AsPublicDelivery();

        api.MapPut("/{id}", async (string id, UpdateMediaRequest req, MediaService media) =>
        {
            var updated = await media.UpdateMetadataAsync(id, req.AltText, req.FocalPoints, req.Data);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        });

        api.MapDelete("/{id}", async (string id, MediaService media) =>
            await media.DeleteAsync(id) ? Results.NoContent() : Results.NotFound());
    }
}

public record UpdateMediaRequest(string? AltText, List<MediaFocalPoint>? FocalPoints, Dictionary<string, string>? Data);
