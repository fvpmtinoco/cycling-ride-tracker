using System.Buffers.Text;
using Cycling.Rider.Tracking.Application.Abstractions.Data;
using Cycling.Rider.Tracking.Application.Abstractions.Messaging;
using Cycling.Rider.Tracking.Application.Abstractions.Validation;
using Cycling.Rider.Tracking.SharedKernel;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Cycling.Rider.Tracking.Application.Files;

public sealed record ListFilesQuery(
    string? Cursor,
    int PageSize,
    DateTimeOffset? CreatedAtFrom,
    DateTimeOffset? CreatedAtTo) : IQuery<ListFilesResult>;

public sealed record FileListItem(Guid Id, DateTimeOffset RideDate, DateTimeOffset CreatedAt);
public sealed record ListFilesResult(IReadOnlyList<FileListItem> Items, string? NextCursor);

public sealed class ListFilesQueryValidator : AbstractValidator<ListFilesQuery>
{
    public ListFilesQueryValidator()
    {
        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, 100)
            .WithErrorCode("PageSize.OutOfRange")
            .WithMessage("Page size must be between 1 and 100.");

        RuleFor(query => query)
            .Must(query => query.CreatedAtFrom <= query.CreatedAtTo)
            .When(query => query.CreatedAtFrom is not null && query.CreatedAtTo is not null)
            .WithName("createdAtFrom")
            .WithErrorCode("CreatedAt.Range")
            .WithMessage("createdAtFrom must be earlier than or equal to createdAtTo.");
    }
}

public sealed class ListFilesQueryHandler(
    IDatabaseContext databaseContext,
    IValidator<ListFilesQuery> validator) : IQueryHandler<ListFilesQuery, ListFilesResult>
{
    public async Task<Result<ListFilesResult>> HandleAsync(ListFilesQuery query, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<ListFilesResult>(validation.ToValidationError());
        }

        Guid? cursorId = null;
        if (!string.IsNullOrEmpty(query.Cursor))
        {
            if (!TryDecodeCursor(query.Cursor, out Guid decoded))
            {
                return Result.Failure<ListFilesResult>(
                    new ValidationError([new Error("Cursor.Invalid", "The cursor is invalid.", ErrorType.Validation)]));
            }

            cursorId = decoded;
        }

        IQueryable<Domain.Rides.Ride> rides = databaseContext.Rides.AsNoTracking();

        if (cursorId is Guid afterId)
        {
            // Keyset pagination: Id is a time-ordered Guid v7, so "id < cursor" walks to older rows.
            rides = rides.Where(ride => ride.Id.CompareTo(afterId) < 0);
        }

        if (query.CreatedAtFrom is DateTimeOffset from)
        {
            rides = rides.Where(ride => ride.CreatedAt >= from);
        }

        if (query.CreatedAtTo is DateTimeOffset to)
        {
            rides = rides.Where(ride => ride.CreatedAt <= to);
        }

        // Fetch one extra row to detect whether a further page exists.
        List<FileListItem> items = await rides
            .OrderByDescending(ride => ride.Id)
            .Take(query.PageSize + 1)
            .Select(ride => new FileListItem(ride.Id, ride.RideDate, ride.CreatedAt))
            .ToListAsync(cancellationToken);

        string? nextCursor = null;
        if (items.Count > query.PageSize)
        {
            items.RemoveAt(items.Count - 1);
            nextCursor = EncodeCursor(items[^1].Id);
        }

        return Result.Success(new ListFilesResult(items, nextCursor));
    }

    private static string EncodeCursor(Guid id) =>
        Base64Url.EncodeToString(System.Text.Encoding.UTF8.GetBytes(id.ToString()));

    private static bool TryDecodeCursor(string cursor, out Guid id)
    {
        id = Guid.Empty;
        try
        {
            byte[] bytes = Base64Url.DecodeFromChars(cursor);
            return Guid.TryParse(System.Text.Encoding.UTF8.GetString(bytes), out id);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
