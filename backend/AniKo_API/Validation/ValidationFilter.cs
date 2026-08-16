using FluentValidation;

namespace AniKo_API.Validation;

/// <summary>
/// Runs the registered <see cref="IValidator{T}"/> for a request record before the handler sees
/// it, and turns a failure into RFC 9457 <c>ValidationProblem</c> (400) rather than letting the
/// handler run on unchecked input.
/// </summary>
/// <remarks>
/// <para>
/// Minimal APIs do <b>not</b> run FluentValidation for you. There is no <c>[ApiController]</c>
/// model-state gate here — <c>AddValidatorsFromAssemblyContaining</c> registers the validators in
/// DI and nothing else invokes them. A validator that exists, compiles, is registered, and is
/// fully unit-tested still never runs unless something like this filter is attached to the
/// endpoint. That is the whole reason this type exists, and it is worth stating because the
/// failure mode is an endpoint that accepts <c>months=999</c> with a green test suite.
/// </para>
/// <para>
/// <b>The missing-argument case throws instead of passing.</b> The filter finds the request by
/// scanning <see cref="EndpointFilterInvocationContext.Arguments"/> for one assignable to
/// <typeparamref name="TRequest"/>. If the handler signature or the binding shape changes so that
/// no such argument exists, the tempting behaviour is to shrug and call <c>next</c> — which
/// silently disables validation on that endpoint while every existing test still passes, because
/// they all send valid input. Throwing turns a refactor into a 500 on the first request, which is
/// noisy, immediate, and traceable to this file.
/// </para>
/// </remarks>
/// <typeparam name="TRequest">The bound request record, e.g. <c>PriceTrendsRequest</c>.</typeparam>
public sealed class ValidationFilter<TRequest>(IValidator<TRequest> validator) : IEndpointFilter
    where TRequest : notnull
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var request = context.Arguments.OfType<TRequest>().FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"No argument of type {typeof(TRequest).Name} was bound for " +
                $"{context.HttpContext.Request.Path}. ValidationFilter<{typeof(TRequest).Name}> " +
                "is attached to an endpoint whose handler does not take that type — validation " +
                "would otherwise be skipped without any visible symptom.");

        var result = await validator.ValidateAsync(request, context.HttpContext.RequestAborted);
        if (result.IsValid)
        {
            return await next(context);
        }

        // ToDictionary over a grouping, not over the errors directly: two rules can fail on the
        // same property (`limit` both non-numeric-coerced and out of range), and a plain
        // ToDictionary on a duplicate key throws — turning a 400 into a 500 only for callers who
        // got it wrong in two ways at once, which is the least likely case to be tested.
        return TypedResults.ValidationProblem(result.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));
    }
}

/// <summary>Attaches <see cref="ValidationFilter{TRequest}"/> and its OpenAPI 400 response.</summary>
public static class ValidationFilterExtensions
{
    /// <summary>
    /// Adds the validation filter for <typeparamref name="TRequest"/> and declares the 400 it can
    /// produce, so the two cannot drift apart — an endpoint that validates but does not advertise
    /// <c>ValidationProblemDetails</c> generates a client that treats a 400 body as unparseable.
    /// </summary>
    public static RouteHandlerBuilder WithValidation<TRequest>(this RouteHandlerBuilder builder)
        where TRequest : notnull =>
        builder
            .AddEndpointFilter<ValidationFilter<TRequest>>()
            .ProducesValidationProblem();
}
