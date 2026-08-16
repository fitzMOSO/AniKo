namespace AniKo_API.Services;

/// <summary>
/// Great-circle distance between two points on the earth, in kilometres.
/// </summary>
/// <remarks>
/// <para>
/// Static and dependency-free because distance is a function of four numbers and nothing else.
/// It is separated from <see cref="NearbySupplierService"/> so that the formula can be checked
/// against distances a human can verify on a map — Manila to Cebu is about 570 km, and a test
/// that says so catches a swapped latitude/longitude pair, a degrees-vs-radians slip and a
/// wrong earth radius, none of which throw.
/// </para>
/// <para>
/// A sphere, not the WGS-84 ellipsoid. Haversine is off by up to about 0.5% against a Vincenty
/// solution, which over the ~500 km spans this endpoint deals with is a couple of kilometres.
/// The figure is rendered as "12.4 km away" next to a supplier card and used only to rank six
/// rows; half a percent does not change the ranking and does not change what the buyer reads.
/// </para>
/// </remarks>
public static class GeoDistance
{
    /// <summary>
    /// Mean earth radius in kilometres. Fixing the radius here is what fixes the unit of the
    /// return value — the formula itself yields an angle, and the radius is the only thing that
    /// turns it into a length.
    /// </summary>
    public const double EarthRadiusKm = 6371.0;

    /// <summary>
    /// Haversine distance between two decimal-degree coordinates, in kilometres.
    /// </summary>
    public static double KilometresBetween(double lat1, double lng1, double lat2, double lng2)
    {
        var phi1 = ToRadians(lat1);
        var phi2 = ToRadians(lat2);
        var deltaPhi = ToRadians(lat2 - lat1);
        var deltaLambda = ToRadians(lng2 - lng1);

        var sinLat = Math.Sin(deltaPhi / 2.0);
        var sinLng = Math.Sin(deltaLambda / 2.0);

        var h = (sinLat * sinLat) + (Math.Cos(phi1) * Math.Cos(phi2) * sinLng * sinLng);

        // The clamp is the whole reason this is not a one-liner, and it guards a real failure
        // rather than a theoretical one.
        //
        // `h` is the haversine of the central angle, so it is mathematically in [0, 1] and
        // `Math.Asin(Math.Sqrt(h))` is always defined. In double arithmetic it is not: for two
        // points that are exactly antipodal the three terms sum to 1.0000000000000002, and for
        // two *identical* points expressed via the law-of-cosines form the same rounding pushes
        // the cosine past 1.0. Either way `Asin`/`Acos` is handed an out-of-domain argument and
        // returns NaN — and NaN propagates: it serialises to JSON as `null` (or throws,
        // depending on the serialiser), it compares false against everything so `OrderBy` puts
        // the row somewhere arbitrary, and nothing anywhere logs an error. A supplier simply
        // appears in the wrong place in the list.
        //
        // Clamping costs one comparison and removes the entire class of failure.
        h = Math.Clamp(h, 0.0, 1.0);

        var centralAngle = 2.0 * Math.Asin(Math.Sqrt(h));

        return EarthRadiusKm * centralAngle;
    }

    /// <summary>
    /// Convenience overload for the common case of measuring to a supplier's stored position.
    /// </summary>
    public static double KilometresBetween(
        (double Lat, double Lng) from,
        (double Lat, double Lng) to) =>
        KilometresBetween(from.Lat, from.Lng, to.Lat, to.Lng);

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
}
