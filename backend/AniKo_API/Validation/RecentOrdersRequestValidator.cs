using AniKo_API.Dtos;
using FluentValidation;

namespace AniKo_API.Validation;

/// <summary>
/// Guards <c>GET /api/v1/orders/recent?limit=</c>. See
/// <see cref="FeaturedLotsRequestValidator"/> for why this is a separate, nearly empty class.
/// </summary>
public class RecentOrdersRequestValidator : AbstractValidator<RecentOrdersRequest>
{
    public RecentOrdersRequestValidator()
    {
        RuleFor(request => request.Limit).ValidLimit();
    }
}
