# TrafficNova Pro — Release Notes

## v1.0.0 — 2026-05-18

### Overview
First public release of TrafficNova Pro — a professional web traffic bot built on .NET 10 + WPF with Microsoft Playwright.

### Features

**Core Engine**
- Playwright-powered browser automation (Chromium, headless and headed)
- Configurable thread count, dwell time, bounce rate, and visit targets
- Stealth mode (Low / Medium / High): canvas noise, WebGL spoof, random viewport
- Screenshot on error (saved to `%AppData%\TrafficNovaPro\screenshots`)
- Per-domain rate limiter and session throttle
- Warm-up visits before main campaign traffic
- Watchdog service: auto-stop stalled campaigns after 3 minutes

**Campaign Management**
- Multiple target URLs per campaign (round-robin distribution)
- Referrer modes: None, Direct, Google, Bing, Social, Custom
- Referrer keyword support for Google/Bing SERP simulation
- Device types: Desktop, Mobile, Tablet (matching UA and viewport)
- Geo-spoofing: 24 country profiles (locale, timezone, Accept-Language)
- Custom HTTP headers per campaign
- Campaign scheduler with cron expressions (Cronos-based)
- Import/export campaigns as JSON
- Bulk start/stop/delete actions

**Proxy Manager**
- Import proxies (IP:Port, IP:Port:User:Pass, URI format)
- Auto-test proxies on import
- Proxy health monitor (background, configurable interval)
- Dead-proxy threshold with desktop notifications
- Proxy chain stub (UI ready, backend wired)

**Analytics & Dashboard**
- Live dashboard: session rate, success rate, proxy health gauge
- LiveChartsCore time-series chart (sessions per 15-minute bucket)
- Session log with search, date filter, export (CSV, Excel, JSON)
- Per-campaign stats (visit count, error count, elapsed time)

**Scheduler**
- Cron job scheduler with Cron Builder dialog (presets + manual)
- Next 5 occurrences preview
- Run-now, enable/disable, max-runs enforcement
- Timezone-aware scheduling

**Settings**
- Full settings page (7 tabs): General, Browser, Proxy, Scheduler, Notifications, Logging, License
- Light / Dark theme with runtime switching
- Export/import settings as JSON
- FlareSolverr integration URL (bypass Cloudflare challenges)

**System Integration**
- System tray icon with session count badge
- Minimize-to-tray on close (configurable)
- Start All / Stop All from tray menu
- Auto-update check on startup (GitHub releases API)
- First-run onboarding wizard

**Quality**
- xUnit test suite: UrlValidator, ReferrerGenerator, UserAgentPool, SchedulerService
- Serilog structured logging with daily rolling files
- Global exception handler + crash dump writer
- Inno Setup 6 installer script (Step 118)
- Self-contained win-x64 publish script

### Known Limitations
- Proxy chain (double-hop) is UI-only stub; backend routing not yet implemented
- FlareSolverr bypass integration requires a running FlareSolverr instance
- Auto-update downloads require manual installation (in-place update deferred to v1.1)
- Mobile device emulation uses UA + viewport spoofing; no real mobile device required
- Scheduler timezone conversion uses local system timezone list

### System Requirements
- Windows 10 (1809) or later (x64)
- .NET 10 Desktop Runtime
- Chromium installed or bundled via Playwright
