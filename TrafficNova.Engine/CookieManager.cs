using Microsoft.Playwright;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TrafficNova.Core.Interfaces;
using TrafficNova.Core.Models;

namespace TrafficNova.Engine;

/// <summary>
/// Persists and restores per-campaign cookies to/from the Campaign.CookiesBlob column.
/// Only active when AcceptCookies = true on the campaign.
/// </summary>
public class CookieManager
{
    private readonly ICampaignService _campaigns;
    private readonly ILogger<CookieManager> _log;

    public CookieManager(ICampaignService campaigns, ILogger<CookieManager> log)
    {
        _campaigns = campaigns;
        _log       = log;
    }

    /// <summary>Serialises cookies from the context and saves to Campaign.CookiesBlob.</summary>
    public async Task StoreCookiesAsync(IBrowserContext context, int campaignId)
    {
        try
        {
            var cookies = await context.CookiesAsync();
            if (cookies is null || cookies.Count == 0) return;

            var campaign = await _campaigns.GetByIdAsync(campaignId);
            if (campaign is null) return;

            campaign.CookiesBlob = JsonConvert.SerializeObject(cookies);
            await _campaigns.UpdateAsync(campaign);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Cookie store failed for campaign {Id}", campaignId);
        }
    }

    /// <summary>Loads previously saved cookies into the browser context.</summary>
    public async Task LoadCookiesAsync(IBrowserContext context, int campaignId)
    {
        try
        {
            var campaign = await _campaigns.GetByIdAsync(campaignId);
            if (campaign?.CookiesBlob is null) return;

            var cookies = JsonConvert.DeserializeObject<List<Cookie>>(campaign.CookiesBlob);
            if (cookies is { Count: > 0 })
                await context.AddCookiesAsync(cookies);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Cookie load failed for campaign {Id}", campaignId);
        }
    }
}
