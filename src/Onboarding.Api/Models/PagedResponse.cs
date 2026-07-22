namespace Onboarding.Api.Models;

public sealed class PagedResponse<T>
{
    public IReadOnlyCollection<T> Items { get; set; } =
        Array.Empty<T>();

    public int PageNumber { get; set; }

    public int PageSize { get; set; }

    public int TotalItems { get; set; }

    public int TotalPages { get; set; }

    public bool HasPreviousPage =>
        PageNumber > 1;

    public bool HasNextPage =>
        PageNumber < TotalPages;
}