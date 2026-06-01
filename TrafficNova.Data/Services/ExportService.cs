using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using TrafficNova.Core.Interfaces;
using TrafficNova.Core.Models;

namespace TrafficNova.Data.Services;

public class ExportService
{
    private readonly ILogger<ExportService> _log;

    public ExportService(ILogger<ExportService> log)
    {
        _log = log;
        // EPPlus 7 requires license context
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
    }

    private static string DownloadsPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

    public Task<string> ExportSessionsToCsvAsync(IList<TrafficSession> sessions)
    {
        Directory.CreateDirectory(DownloadsPath);
        var path = Path.Combine(DownloadsPath,
            $"sessions_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

        var sb = new StringBuilder();
        sb.AppendLine("Id,CampaignId,TargetUrl,StartedAt,EndedAt,DurationMs,Success,StatusCode,DwellMs,BlockedRequests,ProxyId,ErrorMessage");
        foreach (var s in sessions)
        {
            sb.AppendLine(string.Join(",",
                s.Id, s.CampaignId,
                CsvEscape(s.TargetUrl),
                s.StartedAt.ToString("o"),
                s.EndedAt?.ToString("o") ?? "",
                s.DurationMs?.ToString() ?? "",
                s.Success, s.StatusCode?.ToString() ?? "",
                s.DwellMs,
                s.BlockedRequests,
                s.ProxyId?.ToString() ?? "",
                CsvEscape(s.ErrorMessage ?? "")));
        }

        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        _log.LogInformation("Sessions exported to CSV: {Path}", path);
        return Task.FromResult(path);
    }

    public Task<string> ExportSessionsToExcelAsync(IList<TrafficSession> sessions)
    {
        Directory.CreateDirectory(DownloadsPath);
        var path = Path.Combine(DownloadsPath,
            $"sessions_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");

        using var pkg  = new ExcelPackage();
        var ws = pkg.Workbook.Worksheets.Add("Sessions");

        // Header row
        var headers = new[]
        {
            "ID","Campaign ID","URL","Started","Ended","Duration (ms)",
            "Success","HTTP Status","Dwell (ms)","Blocked Requests","Proxy ID","Error"
        };
        for (int i = 0; i < headers.Length; i++)
        {
            ws.Cells[1, i + 1].Value = headers[i];
            ws.Cells[1, i + 1].Style.Font.Bold = true;
            ws.Cells[1, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
            ws.Cells[1, i + 1].Style.Fill.BackgroundColor.SetColor(
                System.Drawing.Color.FromArgb(37, 99, 235));
            ws.Cells[1, i + 1].Style.Font.Color.SetColor(System.Drawing.Color.White);
        }

        // Data rows
        for (int row = 0; row < sessions.Count; row++)
        {
            var s = sessions[row];
            var r = row + 2;
            ws.Cells[r, 1].Value  = s.Id;
            ws.Cells[r, 2].Value  = s.CampaignId;
            ws.Cells[r, 3].Value  = s.TargetUrl;
            ws.Cells[r, 4].Value  = s.StartedAt.ToString("yyyy-MM-dd HH:mm:ss");
            ws.Cells[r, 5].Value  = s.EndedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "";
            ws.Cells[r, 6].Value  = s.DurationMs;
            ws.Cells[r, 7].Value  = s.Success ? "Yes" : "No";
            ws.Cells[r, 8].Value  = s.StatusCode;
            ws.Cells[r, 9].Value  = s.DwellMs;
            ws.Cells[r, 10].Value = s.BlockedRequests;
            ws.Cells[r, 11].Value = s.ProxyId;
            ws.Cells[r, 12].Value = s.ErrorMessage ?? "";
        }

        ws.Cells[ws.Dimension.Address].AutoFitColumns();
        pkg.SaveAs(new FileInfo(path));
        _log.LogInformation("Sessions exported to Excel: {Path}", path);
        return Task.FromResult(path);
    }

    public Task<string> ExportProxyStatsToCsvAsync(IList<ProxyEntry> proxies)
    {
        Directory.CreateDirectory(DownloadsPath);
        var path = Path.Combine(DownloadsPath,
            $"proxy_stats_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

        var sb = new StringBuilder();
        sb.AppendLine("Id,Host,Port,Protocol,Group,Active,SuccessCount,FailureCount,SuccessRate%,AvgResponseMs,LastTested");
        foreach (var p in proxies)
        {
            sb.AppendLine(string.Join(",",
                p.Id, CsvEscape(p.Host), p.Port, p.Protocol, CsvEscape(p.GroupTag),
                p.IsActive,
                p.SuccessCount, p.FailureCount,
                (p.SuccessRate * 100).ToString("F1"),
                p.AvgResponseMs,
                p.LastTestedAt?.ToString("o") ?? ""));
        }

        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        _log.LogInformation("Proxy stats exported: {Path}", path);
        return Task.FromResult(path);
    }

    private static string CsvEscape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
