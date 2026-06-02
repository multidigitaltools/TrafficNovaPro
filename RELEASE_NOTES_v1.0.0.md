# TrafficNova Pro v1.0.0 — Release Notes

**Release Date:** 2026-06-02  
**Publisher:** MultiDigitalTools  
**Platform:** Windows 10/11 (64-bit)

## What's New (Initial Release)

### Core Engine
- Microsoft Playwright 1.x browser automation (Chromium + Firefox)
- Unlimited parallel sessions with independent browser contexts
- Real fingerprint rotation — undetectable as automated traffic

### Campaign Manager  
- Create and manage multiple traffic campaigns
- Per-campaign proxy assignment and rotation
- Resource blocking: None / Media / Aggressive modes (up to 75% bandwidth reduction)
- Visual Campaign Editor dialog

### Proxy Manager
- HTTP, HTTPS, SOCKS5 proxy support
- Bulk import (IP:Port or IP:Port:User:Pass format)
- One-click validation with latency + geo-tag display

### Analytics Dashboard
- Real-time LiveCharts2 charts: visits/min, success rate, bandwidth
- 2.5s auto-refresh, campaign progress bars inline in grid

### Scheduler
- Cron-based campaign scheduling with visual GUI
- Powered by Cronos library

### Session Viewer
- Full session history with double-click detail view
- Screenshot + network trace per session

### Trial & Licensing
- 14-day full-functionality trial
- Day-7 and day-14 expiry notifications
- License activation via Settings panel

### Technical
- .NET 10 + WPF + HandyControl 3.x
- SQLite via EF Core 9 (local data, no cloud)
- Serilog structured logging
- Clean Inno Setup 6 installer (38 MB, bundled .NET runtime)

## System Requirements
- Windows 10 or 11 (64-bit)
- 4 GB RAM minimum (8 GB recommended for high concurrency)
- 500 MB disk space + Playwright browser download (~300 MB, one-time)
- Internet connection required for Playwright browser download on first run
