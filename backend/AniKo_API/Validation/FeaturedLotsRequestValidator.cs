using AniKo_API.Dtos;
using FluentValidation;

namespace AniKo_API.Validation;

/// <summary>
/// Guards <c>GET /api/v1/listings/featured?limit=</c>.
/// </summary>
/// <remarks>
/// Thin on purpose — the rule lives in <see cref="DashboardValidationRules.ValidLimit{T}"/> so the
/// three <c>limit</c>-taking endpoints cannot drift apart. The class still exists per request type
/// because that is what <c>AddValidatorsFromAssembly</c> resolves against, and because the day
/// featured lots grows a second parameter, there is somewhere obvious for it to go.
/// </remarks>
public class FeaturedLotsRequestValidator : AbstractValidator<FeaturedLotsRequest>
{
    public FeaturedLotsRequestValidator()
    {
        RuleFor(request => request.Limit).ValidLimit();
    }
}
