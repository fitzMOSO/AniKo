using AniKo_API.Dtos;
using FluentValidation;

namespace AniKo_API.Validation;

/// <summary>
/// Guards <c>GET /api/v1/pricing/trends?months=</c>.
/// <para>
/// The failure this prevents is not a crash. <c>months=0</c> returns an empty chart that looks
/// like "no price data" rather than "you asked for zero months"; <c>months=999</c> returns the
/// same 24 months a clamp would give and leaves the caller believing they have four years of
/// history on screen. Both render, both return 200, and neither logs anything. A 400 naming the
/// range is the only version of this the caller can act on.
/// </para>
/// </summary>
public class PriceTrendsRequestValidator : AbstractValidator<PriceTrendsRequest>
{
    public PriceTrendsRequestValidator()
    {
        RuleFor(request => request.Months)
            .InclusiveBetween(DashboardRequestBounds.MinMonths, DashboardRequestBounds.MaxMonths)
            .OverridePropertyName("months")
            .WithMessage(DashboardValidationRules.RangeMessage(
                "months",
                DashboardRequestBounds.MinMonths,
                DashboardRequestBounds.MaxMonths));
    }
}
