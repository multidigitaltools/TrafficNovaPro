using TrafficNova.Core.Models;

namespace TrafficNova.Core.Helpers;

/// <summary>Generates a realistic HTTP Referer header value for a given mode.</summary>
public static class ReferrerGenerator
{
    // Random.Shared is thread-safe; a plain shared `new Random()` is not, and
    // this is reached concurrently while multiple campaigns build their visits.
    private static readonly Random _rng = Random.Shared;

    private static readonly string[] _googleTlds =
        ["google.com", "google.co.uk", "google.de", "google.fr", "google.ca",
         "google.com.au", "google.co.in", "google.com.br", "google.co.jp"];

    private static readonly string[] _bingDomains =
        ["bing.com", "bing.com/search"];

    private static readonly string[] _socialUrls =
    [
        "https://www.facebook.com/",
        "https://t.co/",
        "https://twitter.com/",
        "https://www.reddit.com/",
        "https://www.pinterest.com/",
        "https://www.linkedin.com/feed/",
        "https://www.instagram.com/",
        "https://news.ycombinator.com/",
        "https://www.quora.com/",
        "https://medium.com/",
    ];

    private static readonly string[] _defaultKeywords =
    [
        "best", "how to", "review", "top", "guide", "buy", "cheap", "online",
        "free", "download", "tutorial", "compare", "vs", "alternative",
    ];

    /// <summary>
    /// Returns a referrer URL string (may be empty/null for Direct/None modes).
    /// </summary>
    public static string? Generate(
        ReferrerMode mode,
        string? customReferrer = null,
        string? keywords = null,
        string? targetHost = null)
    {
        return mode switch
        {
            ReferrerMode.None    => null,
            ReferrerMode.Direct  => null,
            ReferrerMode.Google  => BuildGoogleReferrer(keywords, targetHost),
            ReferrerMode.Bing    => BuildBingReferrer(keywords),
            ReferrerMode.Social  => _socialUrls[_rng.Next(_socialUrls.Length)],
            ReferrerMode.Custom  => customReferrer,
            _                    => null,
        };
    }

    private static string BuildGoogleReferrer(string? keywords, string? targetHost)
    {
        var tld   = _googleTlds[_rng.Next(_googleTlds.Length)];
        var query = BuildQuery(keywords, targetHost);
        return $"https://www.{tld}/search?q={Uri.EscapeDataString(query)}&hl=en&source=hp";
    }

    private static string BuildBingReferrer(string? keywords)
    {
        var query = BuildQuery(keywords, null);
        return $"https://www.bing.com/search?q={Uri.EscapeDataString(query)}";
    }

    private static string BuildQuery(string? keywords, string? targetHost)
    {
        // Use provided keywords if any
        if (!string.IsNullOrWhiteSpace(keywords))
        {
            var kws = keywords!
                .Split(['\n', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(k => k.Length > 0)
                .ToArray();

            if (kws.Length > 0)
                return kws[_rng.Next(kws.Length)];
        }

        // Fall back to generic keyword + optional domain hint
        var word = _defaultKeywords[_rng.Next(_defaultKeywords.Length)];
        return string.IsNullOrEmpty(targetHost)
            ? word
            : $"{word} site:{targetHost}";
    }
}
