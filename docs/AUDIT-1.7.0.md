# 1.7.0 release audit

Scope: native UI changes, all page layouts and navigation, local UI preference storage, Skia integration and packaging, existing privileged-operation entry points, and the release workflow. This is a source/dependency review plus automated testing, not a penetration test or a guarantee that all historical code is free of defects.

## Findings addressed

| Finding | Resolution |
|---|---|
| Back routing hard-coded unrelated parents and omitted most pages | Bounded route history with synchronized sidebar and disabled Back at the root |
| Long English labels, fixed table widths, and WSL short-window clipping | Content-width grid adaptation, measured wrapping bars, wrapped labels, outer WSL scrolling |
| Pointer-only feature-card activation | Native Click events and accessible names on icon-only actions |
| Explicit app themes could differ from confirmation dialogs | RequestedTheme propagated to destructive-operation dialogs |
| Repeated close gestures could open concurrent close dialogs | Guard the active close dialog with a finally-reset flag |
| Unsupported XAML `GoBack` key failed startup despite compiling | Removed that key; retain supported Alt+Left; require actual startup smoke testing |
| New native Skia binaries require redistributable notices | Include upstream MIT license and bundled component notices in all packages |
| Transitive package audit was not explicitly enabled | NuGetAuditMode=all plus explicit direct/transitive vulnerability report gate |
| Additional screenshot artifact could accidentally become a release download | Release job downloads only application package artifacts |

## Review observations

- New preference data contains only enum values for appearance and language. Loading validates enum values and handles missing, malformed, and inaccessible files. Saving replaces a temporary file; failures are logged without blocking the UI.
- The chart creates its own drawing paths and reads integer rule counts. It does not decode untrusted images, accept SVG scripts, fetch remote resources, or add telemetry. Native resources use deterministic disposal; rendering is bounded and demand-driven.
- SkiaSharp and its Win32 native dependency are pinned to 3.119.4. Native binaries exist for all three distribution architectures. Package restore and the explicit NuGet vulnerability gate must succeed before release.
- Existing firewall writes use the Windows firewall API; network/SMB scripts escape PowerShell single-quoted inputs and validate configuration. WSL subprocess calls use structured argument lists. The UI change adds no new privileged operation.
- Destructive rule/process/share/distribution actions retain their existing confirmation dialogs. The GUI test never invokes these actions. The Windows firewall integration test retains its dedicated opt-in and cleanup behavior.
- GitHub Actions remain pinned to immutable commits. Only the release job has contents write permission, and version tags are checked against the project version. SHA-256 checksums accompany the six application downloads.

## Evidence and limits

The initial local .NET run passed 56 tests. The Windows candidate run passed the .NET and firewall integration jobs, dependency gates, and all three compilations; its startup check caught the unsupported key before publication. Final release evidence is the successful workflow linked from the v1.7.0 release and its `ui-verification` artifact, including the packaged Skia-render log, page/layout/navigation checks, and actual screenshots.

Rust and Go helper test commands are part of CI, but these small helpers currently contain no unit test cases; successful commands validate compilation only. Automated GUI execution is on x64. ARM64/x86 execution, mixed-monitor DPI, large text, Narrator, high contrast, and OS-specific behavior need the manual acceptance matrix in [UI-AND-VALIDATION.md](UI-AND-VALIDATION.md). Package hashes verify download integrity and are not a code-signing certificate.
