using AniKo_API.Dtos;
using FluentValidation;

namespace AniKo_API.Validation;

/// <summary>
/// The rules three of the four dashboard validators share, written once.
/// <para>
/// Three endpoints take a <c>limit</c> and all three mean the same thing by it. Copying
/// <c>InclusiveBetween(1, 50)</c> into each validator would work until the day the cap moves, at
/// which point two of the three would move and the third would keep its own quiet limit. More to
/// the point, the *message* would drift, and the message is the part a frontend developer actually
/// consumes.
/// </para>
/// </summary>
public static class DashboardValidationRules
{
    /// <summary>
    /// The wording every range failure follows.
    /// </summary>
    /// <remarks>
    /// The message names the query parameter as it appears in the URL (lowercase <c>limit</c>, not
    /// the C# <c>Limit</c>), states both bounds, says they are inclusive, and echoes what was
    /// received. That is the set of facts a caller needs to fix the call without opening this
    /// repository — which is the entire justification for a 400 over a silent clamp. The tests
    /// assert on this text for the same reason.
    /// </remarks>
    internal static string RangeMessage(string parameter, object minimum, object maximum) =>
        $"'{parameter}' must be between {minimum} and {maximum} (inclusive). Received " +
        "{PropertyValue}.";

    /// <summary>
    /// The shared <c>limit</c> rule: an integer in
    /// [<see cref="DashboardRequestBounds.MinLimit"/>, <see cref="DashboardRequestBounds.MaxLimit"/>].
    /// </summary>
    public static IRuleBuilderOptions<T, int> ValidLimit<T>(this IRuleBuilder<T, int> rule) =>
        rule
            .InclusiveBetween(DashboardRequestBounds.MinLimit, DashboardRequestBounds.MaxLimit)
            // The property is `Limit` in C# and `limit` on the wire. Callers see the wire.
            .OverridePropertyName("limit")
            .WithMessage(RangeMessage(
                "limit",
                DashboardRequestBounds.MinLimit,
                DashboardRequestBounds.MaxLimit));

    /// <summary>
    /// A coordinate rule that also rejects <see cref="double.NaN"/> and the infinities.
    /// </summary>
    /// <remarks>
    /// <b>Why this is a <c>Must</c> and not an <c>InclusiveBetween</c>.</b> A non-finite double is
    /// exactly what arrives when a browser sends <c>lat=NaN</c> or <c>lat=Infinity</c> — which is
    /// what a frontend produces when it reads a coordinate out of an empty geolocation result and
    /// does arithmetic on <c>undefined</c>. <c>NaN</c> then behaves unlike every other bad value:
    /// <c>NaN &gt;= -90</c> and <c>NaN &lt;= 90</c> are *both* false, so a naive pair of
    /// comparisons rejects it by accident, while <c>IComparable</c>-based comparisons (what
    /// <c>InclusiveBetween</c> uses) order <c>NaN</c> below everything and reject it for a
    /// different accidental reason. Neither accident is something to depend on. Stating
    /// <see cref="double.IsFinite"/> up front makes the rejection deliberate, and it is asserted in
    /// the tests so that it stays that way.
    /// </remarks>
    public static IRuleBuilderOptions<T, double> ValidCoordinate<T>(
        this IRuleBuilder<T, double> rule,
        string parameter,
        double minimum,
        double maximum) =>
        rule
            .Must(value => double.IsFinite(value) && value >= minimum && value <= maximum)
            .OverridePropertyName(parameter)
            .WithMessage(
                $"'{parameter}' must be a finite number between {minimum} and {maximum} degrees " +
                "(inclusive). Received {PropertyValue}.");
}
