namespace TrafficNova.Core.Helpers;

/// <summary>
/// Maps ISO-3166 country codes to browser locale, timezone, and common language UA string.
/// Used by the campaign engine to geo-spoof browser identity.
/// </summary>
public static class GeoConfig
{
    public record GeoProfile(string Locale, string Timezone, string LangHeader);

    private static readonly Dictionary<string, GeoProfile> _map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["US"] = new("en-US", "America/New_York",    "en-US,en;q=0.9"),
        ["GB"] = new("en-GB", "Europe/London",       "en-GB,en;q=0.9"),
        ["CA"] = new("en-CA", "America/Toronto",     "en-CA,en;q=0.9"),
        ["AU"] = new("en-AU", "Australia/Sydney",    "en-AU,en;q=0.9"),
        ["DE"] = new("de-DE", "Europe/Berlin",       "de-DE,de;q=0.9,en;q=0.8"),
        ["FR"] = new("fr-FR", "Europe/Paris",        "fr-FR,fr;q=0.9,en;q=0.8"),
        ["ES"] = new("es-ES", "Europe/Madrid",       "es-ES,es;q=0.9,en;q=0.8"),
        ["IT"] = new("it-IT", "Europe/Rome",         "it-IT,it;q=0.9,en;q=0.8"),
        ["NL"] = new("nl-NL", "Europe/Amsterdam",   "nl-NL,nl;q=0.9,en;q=0.8"),
        ["PL"] = new("pl-PL", "Europe/Warsaw",       "pl-PL,pl;q=0.9,en;q=0.8"),
        ["BR"] = new("pt-BR", "America/Sao_Paulo",   "pt-BR,pt;q=0.9,en;q=0.8"),
        ["IN"] = new("en-IN", "Asia/Kolkata",        "en-IN,en;q=0.9,hi;q=0.8"),
        ["JP"] = new("ja-JP", "Asia/Tokyo",          "ja-JP,ja;q=0.9,en;q=0.8"),
        ["KR"] = new("ko-KR", "Asia/Seoul",          "ko-KR,ko;q=0.9,en;q=0.8"),
        ["RU"] = new("ru-RU", "Europe/Moscow",       "ru-RU,ru;q=0.9,en;q=0.8"),
        ["UA"] = new("uk-UA", "Europe/Kiev",         "uk-UA,uk;q=0.9,en;q=0.8"),
        ["MX"] = new("es-MX", "America/Mexico_City", "es-MX,es;q=0.9,en;q=0.8"),
        ["AR"] = new("es-AR", "America/Argentina/Buenos_Aires", "es-AR,es;q=0.9,en;q=0.8"),
        ["TR"] = new("tr-TR", "Europe/Istanbul",     "tr-TR,tr;q=0.9,en;q=0.8"),
        ["SG"] = new("en-SG", "Asia/Singapore",      "en-SG,en;q=0.9"),
        ["ID"] = new("id-ID", "Asia/Jakarta",        "id-ID,id;q=0.9,en;q=0.8"),
        ["VN"] = new("vi-VN", "Asia/Ho_Chi_Minh",   "vi-VN,vi;q=0.9,en;q=0.8"),
        ["NG"] = new("en-NG", "Africa/Lagos",        "en-NG,en;q=0.9"),
        ["ZA"] = new("en-ZA", "Africa/Johannesburg", "en-ZA,en;q=0.9"),
    };

    public static IReadOnlyList<string> SupportedCountries => _map.Keys.OrderBy(k => k).ToList();

    /// <summary>Returns the geo profile for a country code, or the US defaults.</summary>
    public static GeoProfile Get(string countryCode) =>
        _map.TryGetValue(countryCode, out var p) ? p : _map["US"];
}
