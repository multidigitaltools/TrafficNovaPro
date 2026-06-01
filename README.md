# TrafficNova Pro

<p align="center">
  <img src="screenshots/page_Dashboard.png" alt="TrafficNova Pro Dashboard" width="700"/>
</p>

<p align="center">
  <strong>Professional web traffic automation built on .NET 10 + Playwright</strong><br/>
  Boost your website traffic with intelligent, stealth-mode browser automation.
</p>

<p align="center">
  <a href="https://github.com/MultiDigitalTools/TrafficNovaPro/releases/latest">
    <img src="https://img.shields.io/badge/Download-v1.0.0-2563EB?style=for-the-badge&logo=windows" alt="Download"/>
  </a>
  <a href="https://multidigitaltools.com/trafficnova">
    <img src="https://img.shields.io/badge/Website-multidigitaltools.com-1B2B4B?style=for-the-badge" alt="Website"/>
  </a>
  <img src="https://img.shields.io/badge/.NET-10.0-512BD4?style=for-the-badge&logo=dotnet" alt=".NET 10"/>
  <img src="https://img.shields.io/badge/Platform-Windows%2010+-0078D4?style=for-the-badge&logo=windows" alt="Windows"/>
</p>

---

## ✨ Features

### 🚀 Powerful Traffic Engine
- **Playwright-powered** browser automation (Chromium, headless & headed)
- Configurable **thread count**, dwell time, bounce rate, and visit targets
- **Stealth modes** (Low / Medium / High): canvas noise, WebGL spoofing, random viewports
- **Warm-up visits** before main campaign traffic
- **Watchdog service**: auto-stop stalled campaigns

### 📋 Campaign Management
- Multiple target URLs per campaign (round-robin distribution)
- **Referrer modes**: None, Direct, Google, Bing, Social, Custom
- **Device types**: Desktop, Mobile, Tablet (matching UA + viewport)
- **Geo-spoofing**: 24 country profiles (locale, timezone, Accept-Language headers)
- Custom HTTP headers per campaign
- Import / export campaigns as JSON

### 🔌 Proxy Manager
- Import proxies: IP:Port, IP:Port:User:Pass, URI formats
- **Auto-test proxies** on import
- **Proxy health monitor** (background, configurable interval)
- Dead-proxy threshold with desktop notifications

### 📊 Live Analytics
- Real-time dashboard: session rate, success rate, proxy health gauge
- **LiveCharts2** time-series chart (sessions per 15-minute bucket)
- Session log with search, date filter, export (CSV, Excel, JSON)
- Per-campaign stats

### ⏰ Scheduler
- **Cron-expression scheduler** with Cron Builder dialog
- Next 5 occurrences preview
- Run-now, enable/disable, max-runs enforcement
- Timezone-aware scheduling

### ⚙️ Settings & Integration
- 7-tab settings page: General, Browser, Proxy, Scheduler, Notifications, Logging, License
- **Light / Dark theme** with runtime switching
- **FlareSolverr** integration (bypass Cloudflare challenges)
- System tray icon with minimize-to-tray

---

## 📸 Screenshots

<table>
  <tr>
    <td><img src="screenshots/page_Dashboard.png" width="300" alt="Dashboard"/><br/><em>Dashboard</em></td>
    <td><img src="screenshots/page_Campaigns.png" width="300" alt="Campaigns"/><br/><em>Campaign Manager</em></td>
  </tr>
  <tr>
    <td><img src="screenshots/page_Proxies.png" width="300" alt="Proxies"/><br/><em>Proxy Manager</em></td>
    <td><img src="screenshots/page_Scheduler.png" width="300" alt="Scheduler"/><br/><em>Scheduler</em></td>
  </tr>
  <tr>
    <td><img src="screenshots/page_Settings.png" width="300" alt="Settings"/><br/><em>Settings</em></td>
    <td><img src="screenshots/page_Logs.png" width="300" alt="Logs"/><br/><em>Session Logs</em></td>
  </tr>
</table>

---

## 💰 Pricing

| Plan | Price | Details |
|------|-------|---------|
| **Free Trial** | $0 | 14 days, full features |
| **Pro License** | Visit website | Node-locked, lifetime updates |

👉 **[Buy Now — multidigitaltools.com/trafficnova](https://multidigitaltools.com/trafficnova#pricing)**

---

## 🎁 Free Trial

TrafficNova Pro includes a **14-day free trial** with **all features unlocked**:

- ✅ Unlimited campaigns
- ✅ Full proxy manager
- ✅ Live analytics
- ✅ All stealth modes
- ✅ Geo-spoofing (24 countries)
- ✅ Campaign scheduler

No credit card required. Trial starts automatically on first launch.

**To activate after trial:** Settings → License tab → Enter your key

---

## 📥 Download

**[⬇️ Download TrafficNova Pro v1.0.0](https://github.com/MultiDigitalTools/TrafficNovaPro/releases/latest)**

**System Requirements:**
- Windows 10 (1809) or later
- [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
- 200 MB disk space
- Internet connection

**SHA256:** `7A5AA11ED31AD1EA4059C81171827DBC209855CC88B1890438ABA3B680839A63`

---

## 🚀 Quick Start

1. Download `TrafficNovaPro_v1.0.0_Setup.exe` from [Releases](../../releases)
2. Run the installer (requires Administrator)
3. Launch TrafficNova Pro from the Desktop or Start Menu
4. Your 14-day trial begins automatically
5. Create your first campaign from the **Campaigns** tab

---

## 🛠️ Tech Stack

| Component | Technology |
|-----------|-----------|
| UI Framework | .NET 10 + WPF |
| UI Controls | HandyControl 3.x |
| Browser Engine | Microsoft Playwright |
| MVVM | CommunityToolkit.Mvvm 8.x |
| Database | SQLite via EF Core 9 |
| Charts | LiveCharts2 |
| Logging | Serilog 4.x |
| Installer | Inno Setup 6 |

---

## 📞 Support

| Channel | Link |
|---------|------|
| 🌐 Website | [multidigitaltools.com](https://multidigitaltools.com) |
| 📧 Email | support@multidigitaltools.com |
| 📖 Docs | [multidigitaltools.com/trafficnova/docs](https://multidigitaltools.com/trafficnova/docs) |
| 🐛 Issues | [GitHub Issues](../../issues) |

---

<p align="center">
  Made with ❤️ by <a href="https://multidigitaltools.com">MultiDigitalTools</a><br/>
  Copyright © 2026 MultiDigitalTools. All rights reserved.
</p>
