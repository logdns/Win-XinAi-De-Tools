# Current audit and validation / 当前审计与验证

当前版本：**1.10.0**。本页区分新增功能的源码审查、候选验证与发布下载校验。
This page summarizes the current implementation and completed validation.

## Hyper-V 1.10.0 focused review / 新增功能专项审查

- Scope: `Services/HyperVService.cs`, `Models/HyperVModels.cs`, Hyper-V page/shell/UI smoke, bilingual strings, regression tests and release metadata. This is a focused source review, not a complete codebase penetration test or an independent audit.
- Elevated PowerShell uses the inbox System32/Sysnative executable, an encoded UTF-16 command, no profile, closed stdin and concurrent stdout/stderr draining. Read-only status has a 45-second timeout; mutations have a 15-minute timeout. User switch names are single-quote escaped; switch types are enum-validated; IDs use GUID values only, normalized to D-format JSON. Wildcard-bearing names are rejected where a cmdlet resolves by name.
- Feature queries target only the three relevant components. Only DISM_E_UNKNOWN_FEATURE (0x800f080c) is treated as Unavailable; permission/servicing failures propagate instead of silently skipping a component. Pending states block mutation; disabled/removed payloads do not trigger unnecessary disable commands.
- Virtual adapters are reclassified by the VMSMP driver and GUID immediately before operations. All selected IDs are preflighted before the first mutation. Physical adapter aliases cannot redirect enable/disable operations.
- External-switch physical adapters are revalidated by GUID/hardware status. Default Switch and other non-prefixed switches cannot be deleted. Switch removal rechecks the tool prefix and rejects attached guest adapters, including powered-off VMs.
- Every mutation requires a Cancel-default confirmation and records success/failure with targets. No VM/distribution/disk deletion, automatic restart, HNS service stop, NAT purge or forced VM shutdown is added. DISM/BCD and adapter batches can partially fail; messages and documentation explicitly require state verification, not assumed rollback.
- Complete disable affects VBS-dependent security and WSL2/Docker. It requests feature/boot changes, not deletion of every virtual network or a guarantee against policy overrides. Runtime and requested next-boot state are shown separately.
- No additional third-party dependency is introduced. Windows CI remains responsible for .NET 8 tests, PowerShell contract fixtures, existing firewall integration, NuGet direct/transitive audit, packaged UI and all six builds. Real Hyper-V mutation/reboot/network recovery is not exercised on hosted runners; see [Hyper-V acceptance](HYPER-V.md#validation--验收边界).
- Earlier evidence below applies only to v1.9.0, not this candidate.

## Verified 1.10.0 candidate / 已完成候选验证

[Successful candidate workflow](https://github.com/logdns/Win-XinAi-De-Tools/actions/runs/38359004541), implementation commit `0f4f40b`, 2026-10-13:

| Check | Result |
|---|---|
| .NET 8 Windows tests | 131 passed; includes live read-only Hyper-V status and absent-feature detection, plus mocked mutation guards and PowerShell syntax |
| Opt-in Windows firewall integration | 2 passed |
| Localization/release metadata | 411 bilingual resources, 15 pages, version 1.10.0 |
| Native x64 UI | 180 page/language/theme/size combinations; actual completed Hyper-V status required, draft retention and existing HTTP checks passed |
| Screenshot review | Actual light/dark Hyper-V page and switch form reviewed; risk text contrast fixed; screenshots include 480/800/1400 requested sizes |
| Dependencies | Direct/transitive NuGet vulnerability checks passed for all three builds; no new package introduced |
| Native helpers | Rust/Go test commands passed (compilation checks; no helper unit cases) |
| Packages | x86/x64/ARM64 portable ZIPs and installers built |

The hosted Windows runner reports three optional-component states and one existing non-tool switch; the switch remains protected. It does not provide physical-device acceptance or prove actual adapter/feature mutations, post-reboot behavior or recovery. Early candidates revealed incomplete status loading, GUID JSON formatting and warning contrast problems; the successful candidate includes the fixes and stronger assertions. The macOS local run uses .NET 9 roll-forward; Windows CI supplies the authoritative .NET 8 results.

Release publication and downloaded checksum verification are recorded only after completion; a successful candidate build alone is not a published release.

## Implementation boundaries

- WinUI 3 renders native controls; SkiaSharp draws the dashboard chart. Theme/language preferences are local. Back follows actual navigation history; running HTTP service lifetime belongs to the application. See [UI behavior](UI-AND-VALIDATION.md).
- Temporary HTTP serves the explicitly selected directory on all IPv4/available IPv6 interfaces. Per-instance inbound TCP firewall rules allow all remote sources and profiles, scoped to the executable and chosen port; Stop/normal Exit removes them. Cleanup failures remain retryable.
- HTTP serves static GET/HEAD content with optional CORS. It rejects writes, traversal, alternate streams, and filesystem links below the root. Windows checks the final path of the opened file handle against the root. Files are streamed; directory listings are escaped and limited to 1000 entries.
- Discovery probes local IPv4/IPv6 TCP listeners using HEAD, preserving IPv6 scope IDs. It disables proxies, redirects, credentials and cookies, reads headers only, and limits concurrency to 16, each request to one second, headers to 16 KiB, and overall probing to ten seconds. Leaving the page cancels detection; obsolete scans cannot replace current results.
- Each address has its own Copy/Open actions. Discovery never controls other services; process information is a snapshot. Plain HTTP confirmation cannot establish whether another server is temporary or publicly reachable.
- CI audits direct/transitive dependencies, pins third-party Actions to commit hashes, and limits repository write permission to release publication. Upstream licenses are included in packages.

## Previously verified release (v1.9.0)

[Successful v1.9.0 workflow](https://github.com/logdns/Win-XinAi-De-Tools/actions/runs/34337521212):

| Check | Result |
|---|---|
| .NET tests | 92 passed |
| Opt-in Windows firewall integration | 2 passed |
| Native UI matrix | 168 page/language/theme/size combinations passed on Windows x64 |
| HTTP GUI checks | Lifecycle, individual clipboard URLs, automatic/manual discovery, independent server, stale-port removal |
| Packages | x86/x64/ARM64 portable ZIPs and installers built |
| Download verification | All six SHA-256 hashes matched; portable architecture, runtime, localization and license contents checked |

Native Rust/Go helper commands also pass; these helpers currently have no unit test cases, so those commands validate compilation. Screenshots in the usage guide come from the packaged Windows app.

## Limits

Plain HTTP is unencrypted and serves files without authentication. HTTPS-only, hostname-only and slow servers may be absent from discovery. A forced process kill or machine crash can leave an executable/port-scoped firewall rule. See [HTTP usage and cleanup](TEMPORARY-HTTP.md).

Public routing, physical x86/ARM64 execution, native folder/browser interactions, mixed DPI and assistive technology need environment-specific acceptance. See the [manual validation matrix](UI-AND-VALIDATION.md#manual-acceptance-matrix). This review is not a penetration test; checksums verify integrity and do not provide code signing.
