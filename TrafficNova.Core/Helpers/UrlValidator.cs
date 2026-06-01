namespace TrafficNova.Core.Helpers;

public static class UrlValidator
{
    private static readonly HashSet<string> _allowedSchemes =
        new(StringComparer.OrdinalIgnoreCase) { "http", "https" };

    /// <summary>Returns true if the value is a well-formed absolute http/https URL.</summary>
    public static bool IsValid(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        return Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
            && _allowedSchemes.Contains(uri.Scheme)
            && !string.IsNullOrEmpty(uri.Host);
    }

    /// <summary>Parses a newline-delimited block of text into valid, deduplicated URLs.</summary>
    public static (IReadOnlyList<string> Valid, IReadOnlyList<string> Invalid)
        ParseBlock(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return (Array.Empty<string>(), Array.Empty<string>());

        var valid   = new List<string>();
        var invalid = new List<string>();
        var seen    = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (IsValid(raw))
            {
                var normalized = raw.Trim().TrimEnd('/');
                if (seen.Add(normalized))
                    valid.Add(normalized);
            }
            else
            {
                invalid.Add(raw);
            }
        }

        return (valid, invalid);
    }

    /// <summary>Normalises a single URL: lowercase scheme+host, strips default port.</summary>
    public static string Normalize(string url)
    {
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
            return url;

        var builder = new UriBuilder(uri)
        {
            Scheme = uri.Scheme.ToLowerInvariant(),
            Host   = uri.Host.ToLowerInvariant(),
        };

        // Remove default ports
        if ((uri.Scheme == "http"  && uri.Port == 80) ||
            (uri.Scheme == "https" && uri.Port == 443))
            builder.Port = -1;

        return builder.Uri.ToString().TrimEnd('/');
    }
}
