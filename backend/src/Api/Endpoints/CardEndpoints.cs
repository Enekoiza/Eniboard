using Application.Dtos;
using Application.Services;
using FluentValidation;

namespace Api.Endpoints;

public static class CardEndpoints
{
    public static void MapCardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/cards").WithTags("Cards").RequireAuthorization();

        group.MapPost("/", async (
            CreateCardRequest request,
            ICardService cardService,
            IValidator<CreateCardRequest> validator,
            CancellationToken cancellationToken) =>
        {
            await validator.ValidateAndThrowAsync(request, cancellationToken);
            var created = await cardService.CreateAsync(request, cancellationToken);
            return Results.Created($"/cards/{created.Id}", created);
        })
        .WithName("CreateCard")
        .Produces<CardResponse>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);

        group.MapGet("/{id:guid}", async (Guid id, ICardService cardService, CancellationToken cancellationToken) =>
        {
            var card = await cardService.GetAsync(id, cancellationToken);
            return Results.Ok(card);
        })
        .WithName("GetCard")
        .Produces<CardResponse>()
        .Produces(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateCardRequest request,
            ICardService cardService,
            IValidator<UpdateCardRequest> validator,
            CancellationToken cancellationToken) =>
        {
            await validator.ValidateAndThrowAsync(request, cancellationToken);
            var updated = await cardService.UpdateAsync(id, request, cancellationToken);
            return Results.Ok(updated);
        })
        .WithName("UpdateCard")
        .Produces<CardResponse>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", async (Guid id, ICardService cardService, CancellationToken cancellationToken) =>
        {
            await cardService.DeleteAsync(id, cancellationToken);
            return Results.NoContent();
        })
        .WithName("DeleteCard")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);

        group.MapPatch("/{id:guid}/move", async (
            Guid id,
            MoveCardRequest request,
            ICardService cardService,
            IValidator<MoveCardRequest> validator,
            CancellationToken cancellationToken) =>
        {
            await validator.ValidateAndThrowAsync(request, cancellationToken);
            var moved = await cardService.MoveCardAsync(id, request.TargetColumnId, request.LinkedBranch, cancellationToken);
            return Results.Ok(moved);
        })
        .WithName("MoveCard")
        .Produces<CardResponse>()
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status409Conflict);
    }
}
