using System.Windows;

namespace TrafficNova.Core.Services;

public enum NotificationLevel { Info, Success, Warning, Error }

/// <summary>
/// Shows HandyControl toast notifications in the UI thread (Step 71).
/// </summary>
public class NotificationService
{
    public void Notify(string title, string message, NotificationLevel level = NotificationLevel.Info)
    {
        // HandyControl's GrowlInfo has no Title field — fold it into the message.
        var text = string.IsNullOrEmpty(title) ? message : $"{title}\n{message}";

        Application.Current?.Dispatcher.Invoke(() =>
        {
            switch (level)
            {
                case NotificationLevel.Success:
                    HandyControl.Controls.Growl.Success(new HandyControl.Data.GrowlInfo
                    {
                        Message  = text,
                        WaitTime = 5,
                    });
                    break;
                case NotificationLevel.Warning:
                    HandyControl.Controls.Growl.Warning(new HandyControl.Data.GrowlInfo
                    {
                        Message  = text,
                        WaitTime = 6,
                    });
                    break;
                case NotificationLevel.Error:
                    HandyControl.Controls.Growl.Error(new HandyControl.Data.GrowlInfo
                    {
                        Message  = text,
                        WaitTime = 8,
                    });
                    break;
                default:
                    HandyControl.Controls.Growl.Info(new HandyControl.Data.GrowlInfo
                    {
                        Message  = text,
                        WaitTime = 4,
                    });
                    break;
            }
        });
    }

    // Convenience helpers
    public void CampaignCompleted(string name) =>
        Notify("Campaign Complete", $"'{name}' finished all visits.", NotificationLevel.Success);

    public void ProxyCountLow(int count) =>
        Notify("Low Proxies", $"Only {count} active proxies remain.", NotificationLevel.Warning);

    public void ErrorRateHigh(double rate) =>
        Notify("High Error Rate", $"Session error rate is {rate:P1} — check proxy health.", NotificationLevel.Warning);

    public void SessionPoolFull() =>
        Notify("Session Pool Full", "All session slots are occupied. New visits are queued.", NotificationLevel.Info);
}
