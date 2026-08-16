using AniKo_API.Dtos;

namespace AniKo_API.Services;

/// <summary>
/// Market price history, pivoted into one row per month carrying every crop.
/// </summary>
public interface IPriceTrendsService
{
    /// <param name="months">Already validated to [1, 24]. The service clamps nothing — an
    /// out-of-range value is a 400 raised by the validator, because a silent clamp turns a
    /// frontend bug into a chart that quietly shows the wrong window.</param>
    /// <param name="cancellationToken">Aborts the underlying queries when the request is abandoned.</param>
    Task<PriceTrendsDto> GetAsync(int months, CancellationToken cancellationToken = default);
}
