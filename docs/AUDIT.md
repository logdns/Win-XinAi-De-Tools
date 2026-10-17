# Current audit and validation / 当前审计与验证

当前候选版本：**1.12.0**；已发布版本：**1.11.1**。本页区分新增功能的源码审查、候选验证与发布下载校验。
This page summarizes the current implementation and completed validation.

## MyGo 1.12.0 functional UI review / 专用功能页修复

The earlier 144-page render checks did not establish that functions were directly visible: the MyGo frontend remained a generic action selector and JSON response panel. This is a product/validation gap, not expected feature parity. Version 1.12.0 replaces it with dedicated native feature views and adds populated-content and real asynchronous read-back checks.

Focused source review covers `mygo/app.go`, `features.go`, `features_state.go`, `transfer.go`, `smoke.go` and the existing dispatcher/model contracts. It is not an independent/full penetration test. Modification calls still require a Cancel-default confirmation; row targets and arguments are frozen; Hyper-V adapter buttons use only the virtual-adapter inventory; protected switch deletion is still enforced by the shared service. Page initialization uses a read-only operation plan and never resets networking, changes Hyper-V or reboots. Failed probes hide cached healthy state. Network drafts are not overwritten by same-adapter refresh; responses for old adapter GUIDs are discarded. Import reads at most 900 KB, previews at most 10 rules and shows a bounded confirmation with rule count and content hash, while the engine retains final validation.

[Successful final candidate workflow](https://github.com/logdns/Win-XinAi-De-Tools/actions/runs/39005023882), implementation `c8efcd2d9077a886c59049a65edfcf1054c02703`, 2026-10-17:

- All 151 Windows .NET 8 tests, 2 opted-in firewall integration tests, 33 Go test functions, 13 installer source contracts, 420-resource bilingual/version audit, native helper builds, pinned-Go vulnerability scans and all three direct/transitive NuGet audits pass. Local Go `-race` and `go vet` also pass.
- MyGo completes 12 initial read-only engine probes plus the selected adapter configuration probe, asserts dedicated native controls with actual Windows data, captures 144 language/theme/size combinations and 20 additional scrolled feature/settings views. Native screenshots for every page are retained as artifacts; both READMEs now show the new rule list, populated IP/DNS form, Hyper-V controls and connection list rather than the obsolete execution console.
- Hosted display constraints are recorded: requested 1400 × 900 yields a 1028 × 749 content capture, 800 × 600 yields 784 × 561 and 480 × 640 yields 464 × 601. Full-width headless tests remain 1400/800/480; these are not physical DPI/accessibility acceptance.
- WinUI repeats 180 native combinations, temporary HTTP lifecycle/cleanup and navigation checks. All ten packages build. Both real x64 installers pass every shortcut selection, Start Menu launch, reinstall and uninstall. The separate simultaneous-installation test verifies that WinUI deselection/reinstall/uninstall preserves MyGo.
- Follow-up review fixes stale success summaries after exceptions, stale/removed adapter selections before reset confirmation, misleading manual DNS controls in DHCP mode and unbounded import confirmation text. Tests assert frozen exact targets, default-cancel behavior, real automatic load/read-back, retained failure status, hidden stale healthy data, draft retention and individual HTTP URL copy/open actions.
- One intermediate candidate (`39004721161`) failed a pre-existing localhost HTTP 301 semantics test because the one-second production probe budget also bounded a cold, busy CI fixture. Response-semantics fixtures now use an internal five-second test timeout; production remains one second with a ten-second overall deadline, no proxy and no redirect following. No failing assertion is skipped or retried into success.
- No real network reset, Hyper-V mutation or reboot is executed. Static IP screenshots show an unsubmitted draft populated from a real DHCP adapter. Physical x86/ARM64, actual reset/reboot recovery, accessibility/mixed DPI, interactive installer presentation and Start Menu indexing remain manual acceptance. Release tag/download verification is a separate step; no old tag is retargeted.


## Verified release 1.11.1 / 双版本发布与下载校验

[Successful tag workflow](https://github.com/logdns/Win-XinAi-De-Tools/actions/runs/39000845077) and [published v1.11.1 release](https://github.com/logdns/Win-XinAi-De-Tools/releases/tag/v1.11.1), verified 2026-10-17:

- Tag resolves to `c6376e8b5fa66010f9282ab5e41d06a6b2aa5e99`; it adds only candidate documentation/screenshots to the validated implementation. The unpublished, cancelled v1.11.0 tag remains `bbc3b493f35ff99713b5e08421ad56bccdd56114`; neither tag is retargeted.
- Tag workflow repeats all 151 Windows .NET 8 tests, 2 firewall integration tests, 13 Go test functions, 13 installer source contracts, 180 WinUI/144 MyGo actual native combinations, production/SDK assertions, NuGet/Go vulnerability checks, helper builds, both real x64 installer lifecycles and simultaneous-installation checks successfully.
- Exactly **11 non-empty assets**: WinUI x86/x64/ARM64 ZIPs and installers, MyGo x64/ARM64 ZIPs and installers, plus one `SHA256SUMS.txt`. The release is public/latest, not a draft or prerelease. Verification screenshots/logs remain Actions artifacts, not release assets.
- Downloaded **all ten packages** and matched every SHA-256 against the published manifest. `scripts/verify-release.py` also passes all five ZIP CRCs, native frontend/engine architectures and administrator manifests, bilingual version/reset resources, self-contained .NET/ASP.NET/WinUI/Skia runtime presence as applicable, recovery guides, Go production/identifier metadata and original Go-module/runtime licenses. This check does not execute installers; actual lifecycle checks are the Windows x64 CI evidence above.
- Both README screenshot sections now use the tag workflow's actual Windows captures, including WinUI network reset and Chinese/English MyGo overview/reset. Release notes describe architecture/capability differences and the bundled engine rather than claiming a small pure-Go executable.
- Real network reset, Hyper-V feature/network mutation, reboot/recovery, physical x86/ARM64, accessibility/mixed DPI, interactive wizard presentation and Start Menu indexing remain manual acceptance. Functional validation and focused source review are not independent penetration testing; hashes are not code signing.

## Production packaging follow-up / 1.11.1 正式构建修订

Package review found that direct `go build` leaves MyGo's development defaults on unless production link flags are supplied. The unpublished v1.11.0 workflow was cancelled before release creation; its tag is retained unchanged. Version 1.11.1 adds `mygo_noinspector`, `production=1`, a distinct SDK package identifier/name/version, explicit per-window DevTools disable and actual Windows smoke assertions for SDK version/production mode. These changes are not a claim of a reproduced cross-principal vulnerability. The production-mode candidate validation is recorded next; tag publication and all downloaded packages are verified below.

## Verified 1.11.1 production candidate / 正式模式候选验证

[Successful workflow](https://github.com/logdns/Win-XinAi-De-Tools/actions/runs/39000195375), implementation `8b3ecb8`, 2026-10-17:

- Repeated all 151 Windows .NET 8 tests, 2 opted-in firewall integration tests, 13 installer source contracts, 420-resource bilingual/metadata audit and all 10 package builds successfully.
- Go production-tag tests pass (13 test functions, all operation forms and 144 headless page combinations); both pinned-Go vulnerability scans and all three direct/transitive NuGet audits pass.
- Actual MyGo x64 smoke records `version=1.11.1; SDK=1.11.1.0; production=true`, requires packaged/production mode and exact release version (only optional `.0` Windows revision), and completes 144 GPU captures plus all 6 read-only engine operations. Inspector exclusion and explicit window DevTools disable are also checked in source/build metadata.
- Actual WinUI x64 smoke repeats 180 combinations and HTTP lifecycle checks. Both editions pass real installation, all shortcut selections, installed-shortcut launch, reinstallation and uninstall; actual simultaneous installation and WinUI uninstall preserving MyGo pass again.
- Production follow-up candidates caught PowerShell native-argument interpolation and Windows VERSIONINFO's four-part version convention. Correct quoting and strict three-/four-part equality tests fix these harness/packaging issues; no version guard is skipped. Actual current screenshots replace the pre-production captures in both READMEs.
- Validation remains Windows x64 execution plus x86/ARM64 build/package checks, not physical-device execution, network reset/reboot or accessibility certification. Release tag/download verification is recorded above; no published tag is retargeted.

## Network reset / MyGo 1.11.0 focused source review

- Scope: new reset service/models/controls, private engine dispatcher, MyGo native frontend, independent installer and release workflow. This is a focused change review, not a full codebase audit, penetration test or independent security certification. Functional builds/tests are separate from this read-only review.
- Reset snapshots are written before mutation; failure aborts. Adapter resets and IP/DNS configuration revalidate a GUID and only mutate its interface index; user-entered aliases/wildcards cannot broaden MyGo network targets. Unknown modes, empty GUIDs and missing/disabled targets are rejected. Fixed System32/Sysnative tools, encoded PowerShell, no shell interpolation of aliases, closed stdin, concurrent output draining and two-minute command deadlines limit the new reset runner. No automatic restart, firewall reset, adapter uninstall, switch removal or HNS/NAT cleanup.
- Winsock/TCP/IP commands have separate exit-code/results. Exit zero does not prove that localized netsh output contains no partial failures. Backups contain sensitive topology and are manual recovery references, not complete backups or rollback. See [network acceptance](NETWORK-RESET.md).
- MyGo starts only the adjacent packaged engine, communicating over private inherited stdio with correlated IDs and a serial operation whitelist; no TCP listener or generic shell endpoint is added. UI and engine versions are checked. Mutation confirmations default to Cancel; the `confirmed` field is not a cross-principal authorization boundary. Both processes are the same administrator. Existing WSL schedule-command functionality intentionally runs user-specified Linux commands after confirmation, not a promise of a command-free application.
- Protocol limits: frontend request 1 MiB, response 32 MiB; engine line length is checked after allocation, suitable for the trusted private parent only, not an untrusted network protocol. General requests time out after five minutes, WSL after one hour, shutdown after fifteen seconds. Forced termination can leave system changes or an executable/port-scoped HTTP rule; inspect locally before retrying.
- Separate AppIds, directories, names, preferences and exact shortcut cleanup prevent intentional cross-edition uninstall/migration. Windows configuration/audit/backups are shared by design; no cross-process system-operation locking is provided. Avoid simultaneous conflicting operations.
- MyGo is pinned to an immutable upstream version; original licenses and notices for all modules/Go are packaged. Initial Go 1.27.1 vulnerability scanning found standard-library advisories; build toolchain is raised to the fixed Go 1.27.2 rather than suppressing findings. MyGo has native controls, but accessibility/DPI and physical ARM64 acceptance are not inherited from WinUI.
- Windows candidate validation is recorded below; tag publication and downloaded-asset verification remain pending. Historical pre-1.11.0 results are not evidence for this version. Real reset, reboot and recovery require a disposable, locally accessible Windows device and are not executed by CI.

## Verified 1.11.0 candidate / 双版本候选验证

[Successful Windows candidate workflow](https://github.com/logdns/Win-XinAi-De-Tools/actions/runs/38998132060), code commit `554620a` (full commit recorded by the workflow), 2026-10-17. The 1.11.0 tag preparation added documentation and actual screenshots only; its unpublished workflow was subsequently cancelled during package review (see 1.11.1 below).

| Check | Result |
|---|---|
| .NET 8 on Windows | 151 tests passed, including GUID/confirmation/result guards and PowerShell parser/mocked-reset contracts; separate opted-in firewall integration: 2 passed |
| MyGo | 12 Go test functions, 144 headless native view combinations, every operation form, minimum-size Cancel-default confirmation, preferences, transport/error contracts; native resource-tool compilation |
| Packaged x64 interfaces | WinUI: 180 combinations plus existing HTTP lifecycle/clipboard/discovery; MyGo: 144 actual captures and 6 read-only engine operations with matching version |
| Localization and metadata | 420 bilingual WinUI resources, 15 WinUI pages, synchronized 1.11.0 metadata across both editions/engine/installers |
| Dependencies | Three direct/transitive NuGet audits and both pinned-Go scans passed; Go 1.27.2 replaces vulnerable 1.27.1; Rust/Go helper compilation checks passed |
| Actual x64 installers | Each edition passed default/none/desktop-only/Start-Menu-only/both selections without postinstall launch, real shortcut targets/icons/workdirs, installed Start Menu launch, reinstallation and uninstall |
| Side-by-side installation | Both real installers coexist; WinUI reinstall/deselection/uninstall preserves MyGo executable/shortcuts; final cleanup removes both editions' links |
| Packages and visuals | All 10 packages built; actual light/dark/compact screenshots reviewed, including corrected dark network warning contrast; source/runtime/physical-device limits remain explicit |

Earlier candidates exposed a mocked PowerShell common-parameter conflict and the new warning's white-text/light-background contrast issue in dark appearance. The final candidate fixes those checks, serializes network refresh, uses GUID-scoped IP/DNS targeting, shows false/partial engine results as failures, and makes smoke/normal result rendering identical. No actual network reset, system reboot or Hyper-V mutation is executed in CI. MyGo's read-only GPU captures do not prove accessibility or physical ARM64 acceptance. This is focused review plus functional validation, not independent penetration testing.

## Installer 1.10.1 focused review / 安装入口专项审查

- Scope: Inno Setup shortcut tasks, exact-path shortcut migration, installer regression harness and workflow. This is a focused source review, not a complete codebase audit.
- Desktop and Start Menu tasks are independent and checked by default; `UsePreviousTasks=no` prevents an old unchecked selection from suppressing a new default. The optional postinstall launch is independent, and silent installation does not launch the app.
- Shortcuts target the installed executable with an explicit installation working directory and embedded application icon. Start Menu uninstall targets Inno's uninstaller. The existing administrator/all-users install identity and architecture restrictions remain unchanged.
- Reinstall cleanup deletes only four exact application-owned `.lnk` paths; no wildcard, recursive directory removal, registry/service/network change or startup registration is added to the installer. Unrelated Start Menu entries are outside that cleanup.
- The regression harness is restricted to elevated disposable Windows CI and refuses pre-existing app registrations, processes and shared shortcut paths. x64 checks execute the installer with all task combinations, read real `.lnk` properties, start the installed app from its shortcut and verify reinstallation/uninstall cleanup. Source-only checks run on every platform.
- Interactive wizard presentation, search indexing and physical x86/ARM64 installation are manual acceptance, not implied by silent x64 testing. The verified 1.10.0 results below remain historical evidence only.

## Verified 1.10.1 candidate / 已完成安装修复候选验证

[Successful candidate workflow](https://github.com/logdns/Win-XinAi-De-Tools/actions/runs/38401799847), implementation commit `a1ff4f1`, 2026-10-13:

| Check | Result |
|---|---|
| Installer source contracts | 6 passed: defaults, task scoping, localization, launch independence, exact migration paths, stable elevated install identity |
| Actual Windows x64 installation | Default, none, desktop-only, Start-Menu-only and both choices passed without postinstall launch; real .lnk targets, icons and working directories checked |
| Installed app startup | Actual Start Menu shortcut launched the installed executable, loaded the dashboard and closed normally |
| Reinstallation and cleanup | Legacy flat shortcut migration, deselecting both options, restoring fresh defaults and all five uninstall checks passed |
| .NET 8 Windows tests | 131 passed; separate elevated firewall integration: 2 passed |
| UI and metadata | 411 bilingual resources, 15 pages, 1.10.1; 180 native UI combinations passed |
| Dependencies and helpers | Three direct/transitive NuGet vulnerability audits and Rust/Go helper compilation checks passed |
| Packages | x86/x64/ARM64 portable ZIPs and installers built |

Early harness runs exposed Windows 8.3/long-path comparison and Inno uninstaller helper timing issues. The successful run normalizes the temporary directory and waits for the uninstaller process tree before reinstalling. These corrections affect the test harness, not the application's feature behavior. Interactive installer display and search indexing remain manual acceptance.

## Verified release 1.10.1 / 安装修复发布与下载校验

[Successful tag workflow](https://github.com/logdns/Win-XinAi-De-Tools/actions/runs/38403144791) and [published v1.10.1 release](https://github.com/logdns/Win-XinAi-De-Tools/releases/tag/v1.10.1), verified 2026-10-13:

- Published tag resolves to `901f2edfe6190d4e335992749ee67706ac4c3dcc`; the commit adds validation documentation/screenshots to the tested implementation. No published tag was retargeted.
- Tag workflow repeated all six installer source contracts, all real x64 installer cases and installed-shortcut launch, 131 Windows tests, two firewall integration tests, 180 native UI combinations, dependency audits, helper checks and all six builds successfully.
- Exactly seven non-empty release assets: three portable ZIPs, three installers and `SHA256SUMS.txt`. The release is neither a draft nor a prerelease. UI/installer verification logs remain Actions artifacts, not downloadable release assets.
- Downloaded all six packages and matched every SHA-256 against the published checksum file. All portable ZIP CRCs, native PE architectures, versioned bilingual resources, included .NET/WinUI/Skia runtimes and license files passed verification. README screenshots now come from the tag workflow.
- Actual installer execution coverage is Windows x64 on the hosted runner. Interactive wizard presentation, Start Menu search indexing, physical x86/ARM64 execution and environment-specific desktop policies remain manual checks. Download checksums are not code signing.

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

## Verified release 1.10.0 / 发布与下载校验

[Successful tag workflow](https://github.com/logdns/Win-XinAi-De-Tools/actions/runs/38360187805) and [published v1.10.0 release](https://github.com/logdns/Win-XinAi-De-Tools/releases/tag/v1.10.0), verified 2026-10-13:

- Published tag resolves to `d4bed972ccd8725dd6c58c2e8379c94ff33fcfe2`. This adds only validation documentation/screenshots to the tested implementation; the published tag has not been retargeted.
- Tag workflow repeated the 131 Windows tests, 2 firewall integration tests, 180 native UI combinations, dependency audits, helper checks and all six builds successfully.
- Exactly seven release assets: three portable ZIPs, three installers and `SHA256SUMS.txt`; no UI verification artifact is exposed as a release asset. All have non-zero sizes and the release is neither a draft nor a prerelease.
- Downloaded all six packages and matched every SHA-256 against the published checksum file. Checked all three portable ZIP CRCs, native executable PE architectures, compiled Hyper-V page/service presence, included .NET/WinUI/Skia runtimes, both localization dictionaries/version strings and license files.
- This is package/integrity verification, not installer execution on every architecture. Physical Hyper-V feature/network mutations and reboot/recovery remain manual acceptance items. Checksums are not code signing.

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
