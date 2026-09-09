# Changelog

## 1.9.0 - 2026-09-09

- Give each temporary HTTP access URL its own Copy and Open actions, including local, IPv4, and IPv6 addresses.
- Automatically detect existing local HTTP servers when opening the page; add manual refresh and refresh after Start/Restart/Stop. List confirmed URLs, HTTP status, port, PID/process when available, and identify the instance managed by this app.
- Probe actual local IPv4/IPv6 TCP listeners with bounded HEAD requests, without proxies, credentials, cookies, redirects, or response-body downloads. Cancel on navigation; distinguish empty, failed, and incomplete scans.
- Add protocol, cancellation, listener/PID, IPv6, clipboard and packaged discovery regression coverage. Update bilingual UI, usage guide, and feature audit.

## 1.8.0 - 2026-09-09

- Add an embedded temporary HTTP file server: default port 8980, selectable directory, open folder/browser, copy addresses, start, stop, and restart.
- Listen on all IPv4/IPv6 interfaces and manage application-scoped temporary inbound TCP firewall rules for all profiles; document public IP, NAT, forwarding, and cloud security-group requirements.
- Serve directory indexes, JSON and other static files with optional CORS; reject traversal, links, and write methods. Keep running across navigation and stop on application exit.
- Add HTTP lifecycle/security tests, real Windows firewall integration, and packaged GUI lifecycle checks; expand layout/theme/language coverage to 14 pages (168 combinations).

## 1.7.0 - 2026-09-08

### Added

- Saved System, Light, and Dark appearance and Chinese/English language preferences.
- A DPI-aware SkiaSharp dashboard chart showing real inbound/outbound firewall rule counts, with accessible native text and demand-driven rendering.
- Windows GUI validation across all 13 pages, two languages, two themes, three window sizes, and all WSL tabs, with screenshots and navigation/draft checks.

### Fixed

- Back now follows actual page history on every route, supports Alt+Left, and keeps sidebar selection synchronized.
- Preserve in-memory tool form state during navigation; retain navigation history when reloading translations.
- Adapt forms and list cards to content width, wrap toolbars and long labels, and keep WSL settings reachable at short window heights.
- Use keyboard-accessible feature-card Click events, accessible names for icon buttons, themed confirmations, and Windows animation preferences.

### Build and audit

- Audit direct and transitive NuGet dependencies; exclude UI screenshots/logs from release downloads.
- Document rendering boundaries, navigation semantics, automated coverage, hardware acceptance checks, and release/rollback procedures.

## 1.6.2 - 2026-09-03

### Fixed

- Decode Linux command output as UTF-8 so disk usage is readable instead of mojibake.
- Determine running state from `wsl --list --running --quiet`, independent of Windows or application display language.
- Keep an always-visible entry for installing additional distributions from the WSL online catalog.
- Add a direct TAR-import entry and a dedicated name field instead of reusing the scheduled-task name.

## 1.6.1 - 2026-09-03

### Changed

- Reorganized WSL management into responsive Overview, Settings, Storage, and Network & USB tabs.
- Removed the redundant introductory WSL information banner and exposed previously clipped settings.
- Added WSL version information, update, and shutdown controls.
- Added an irreversible-data-loss confirmation before unregistering a distribution.

### Fixed

- Wait for elevated WSL installation to finish and report cancellation or a non-zero exit code instead of always reporting that installation started successfully.
- Install a new Ubuntu distribution without launching an interactive first-run shell inside the management workflow.
- Pass external command arguments structurally so spaces and quotes cannot change argument boundaries.
- Preserve non-Unicode operating-system arguments and report startup failures in the optional Rust WSL helper.

### CI and security

- Pin GitHub Actions to immutable commit hashes, move artifact handling to Node.js 24-based actions, restrict write access to the release job, validate tag/version consistency, and test both optional native helpers.
- Treat moderate, high, and critical NuGet vulnerability warnings as build errors.
- Publish SHA-256 checksums alongside release packages.
