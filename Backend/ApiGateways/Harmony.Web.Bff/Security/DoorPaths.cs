namespace Harmony.Web.Bff.Security;

/// <summary>The paths that are doors, never the web app's page: the JSON doors, the proxied API, the well-known documents and the docs page.</summary>
internal static class DoorPaths
{
    public static bool IsDoor(PathString path)
        => path.StartsWithSegments("/bff") || path.StartsWithSegments("/api") || path.StartsWithSegments("/.well-known") || path.StartsWithSegments("/docs");
}
