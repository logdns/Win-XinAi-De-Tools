# Current audit and validation / 当前审计与验证

适用版本：**1.9.0**。本页汇总当前实现及已完成验证，替代分版本的开发过程报告。
This page summarizes the current implementation and completed validation.

## Implementation boundaries

- WinUI 3 renders native controls; SkiaSharp draws the dashboard chart. Theme/language preferences are local. Back follows actual navigation history; running HTTP service lifetime belongs to the application. See [UI behavior](UI-AND-VALIDATION.md).
- Temporary HTTP serves the explicitly selected directory on all IPv4/available IPv6 interfaces. Per-instance inbound TCP firewall rules allow all remote sources and profiles, scoped to the executable and chosen port; Stop/normal Exit removes them. Cleanup failures remain retryable.
- HTTP serves static GET/HEAD content with optional CORS. It rejects writes, traversal, alternate streams, and filesystem links below the root. Windows checks the final path of the opened file handle against the root. Files are streamed; directory listings are escaped and limited to 1000 entries.
- Discovery probes local IPv4/IPv6 TCP listeners using HEAD, preserving IPv6 scope IDs. It disables proxies, redirects, credentials and cookies, reads headers only, and limits concurrency to 16, each request to one second, headers to 16 KiB, and overall probing to ten seconds. Leaving the page cancels detection; obsolete scans cannot replace current results.
- Each address has its own Copy/Open actions. Discovery never controls other services; process information is a snapshot. Plain HTTP confirmation cannot establish whether another server is temporary or publicly reachable.
- CI audits direct/transitive dependencies, pins third-party Actions to commit hashes, and limits repository write permission to release publication. Upstream licenses are included in packages.

## Verified release

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
