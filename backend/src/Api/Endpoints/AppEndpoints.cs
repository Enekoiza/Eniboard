using Application.Dtos;
using Application.Services;
using FluentValidation;

namespace Api.Endpoints;

public static class AppEndpoints
{
    public static void MapAppEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/apps").WithTags("Apps").RequireAuthorization();

        group.MapGet("/", async (IAppService appService, CancellationToken cancellationToken) =>
        {
            var apps = await appService.GetAllAsync(cancellationToken);
            return Results.Ok(apps);
        })
        .WithName("GetApps")
        .Produces<IReadOnlyList<AppResponse>>();

        group.MapPost("/", async (
            CreateAppRequest request,
            IAppService appService,
            IValidator<CreateAppRequest> validator,
            CancellationToken cancellationToken) =>
        {
            await validator.ValidateAndThrowAsync(request, cancellationToken);
            var created = await appService.CreateAppAsync(request, cancellationToken);
            return Results.Created($"/apps/{created.Id}", created);
        })
        .WithName("CreateApp")
        .Produces<AppResponse>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest);

        group.MapGet("/{id:guid}/board", async (Guid id, IAppService appService, CancellationToken cancellationToken) =>
        {
            var board = await appService.GetBoardAsync(id, cancellationToken);
            return Results.Ok(board);
        })
        .WithName("GetAppBoard")
        .Produces<BoardResponse>()
        .Produces(StatusCodes.Status404NotFound);
    }
}
