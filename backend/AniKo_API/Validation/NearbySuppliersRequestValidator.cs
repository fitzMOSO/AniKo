using AniKo_API.Dtos;
using FluentValidation;

namespace AniKo_API.Validation;

/// <summary>
/// Guards <c>GET /api/v1/suppliers/nearby?lat=&amp;lng=&amp;limit=</c>.
/// <para>
/// Coordinates are the one place in this API where a wrong value produces a *confident* wrong
/// answer. Every supplier still comes back, still sorted, still labelled with a distance in
/// kilometres — the distances are simply measured from somewhere the buyer has never been. A
/// transposed pair (<c>lat=121, lng=14</c>) is the common form of this and is caught here only
/// when it pushes latitude past 90; when it does not, nothing but the ordering gives it away,
/// which is why the mapper tests assert the pair is not transposed on the way out as well.
/// </para>
/// </summary>
public class NearbySuppliersRequestValidator : AbstractValidator<NearbySuppliersRequest>
{
    public NearbySuppliersRequestValidator()
    {
        RuleFor(request => request.Lat)
            .ValidCoordinate(
                "lat",
                DashboardRequestBounds.MinLatitude,
                DashboardRequestBounds.MaxLatitude);

        RuleFor(request => request.Lng)
            .ValidCoordinate(
                "lng",
                DashboardRequestBounds.MinLongitude,
                DashboardRequestBounds.MaxLongitude);

        RuleFor(request => request.Limit).ValidLimit();
    }
}
