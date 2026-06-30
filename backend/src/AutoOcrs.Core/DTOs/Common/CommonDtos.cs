namespace AutoOcrs.Core.DTOs.Common;

public record PagedResult<T>(List<T> Data, int Total, int Page, int PageSize)
{
    public int TotalPages => (int)Math.Ceiling(Total / (double)PageSize);
}

public record ApiError(string Message);
