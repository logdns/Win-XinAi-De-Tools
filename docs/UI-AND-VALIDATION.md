# Native interface and validation (1.7.0)

## Rendering and appearance

WinUI 3 supplies the native controls, Mica backdrop, keyboard input, accessibility tree, and page transitions. SkiaSharp supplies the dashboard's anti-aliased rule-distribution chart; this is a Skia raster surface presented as a WinUI bitmap, not a replacement renderer for every native control. No GPU acceleration or frame-rate claim is made.

The chart draws real inbound/outbound rule counts. Native text beside it exposes the same values to screen readers. Empty or failed queries never imply a healthy firewall. Raster dimensions follow the XAML root's DPI scale and are bounded to 2048 × 1024 pixels. The chart redraws only when its counts, pixel dimensions, or theme change; it has no animation timer, releases native drawing resources after rendering, and disconnects from the window on unload. Page transitions respect Windows' animation preference.

The sidebar offers System, Light, and Dark appearance plus Chinese and English language. Preferences are stored in `%LOCALAPPDATA%\Win-XinAi-De-Tools\ui-preferences.json`. A missing, malformed, or unsupported preference falls back to System appearance and Chinese. Writes use a temporary file and replacement; a write failure is logged and does not prevent use. Dialogs inherit the selected appearance. Native file dialogs follow Windows' own appearance settings.

## Navigation contract

- Every page has the shell's Back button. It is disabled when history is empty.
- Back and Alt+Left return to the page actually visited before the current page, including top-level network, SMB, and WSL pages. They do not jump to an unrelated feature category.
- A repeated navigation to the same route does not add history. Unknown routes do nothing. History is limited to the most recent 64 transitions.
- Sidebar selection follows the displayed page. Connection monitor, rule transfer, and audit log highlight More features; clicking that category opens its landing page.
- Add-port, port-query, network, SMB, and WSL pages retain in-memory form state while navigating. Data listing pages reload on entry. Refresh controls retrieve current system state.
- Changing language recreates pages to apply translated resources while retaining route history. **Unsubmitted form values are reset when changing language**, so finish or copy a draft before switching language. Theme changes preserve drafts.
- Feature cards use the native Click event so keyboard activation works as well as pointer activation. Back keyboard accelerators do not navigate behind a modal dialog.

## Responsive layout

All 13 pages share adaptive grid behavior and wrapping action bars. Grid cells collapse in row/column reading order using the grid's available content width, including side-pane consumption. Forms use a 560-DIP breakpoint and list cards use 680 DIP; the dashboard summary uses 520 DIP. Label text can wrap and icon-only action buttons have accessible names.

The navigation pane switches between expanded, compact, and overlay modes. Page padding reduces at 600 DIP of content width. The window can shrink to 480 × 480 native pixels. WSL distribution/detail sections switch to a vertical arrangement on narrow pages; an outer scroll viewer keeps both sections reachable at short heights, while each settings tab retains its own viewport. Existing list virtualization is preserved.

## Automated checks

Run unit tests with:

```powershell
dotnet test Win-XinAi-De-Tools.Tests/Win-XinAi-De-Tools.Tests.csproj -c Release
```

After publishing the Windows x64 application, exercise the real packaged interface:

```powershell
./scripts/ui-smoke.ps1 -Executable artifacts/portable/win-x64/Win-XinAi-De-Tools.exe
```

The harness requires `CI=true` and `--ui-smoke` (set by the script). It only visits pages and changes UI state; it does not click firewall/network/SMB/WSL mutation buttons. Read-only page initialization still queries the host and can write application audit entries. Use a disposable Windows test account: theme/language preferences are changed by the test.

Requested native window sizes can be constrained by the runner display; the log records the actual XAML viewport in DIP for every size.

Coverage: 13 pages × 2 languages × 2 explicit themes × 3 window sizes (480 × 640, 800 × 600, 1400 × 900), all four WSL tabs, layout bounds, selected navigation item, history after changing language, draft retention, unknown routes, empty history, modal-dialog theme/back protection, compact-pane visibility, and restoring System appearance. CI also validates startup, native window/tray icons, minimize/restore, clean shutdown, Windows Firewall integration, Rust/Go helper builds, NuGet vulnerability data for direct/transitive dependencies, and x86/x64/ARM64 portable and installer builds.

The `ui-verification` Actions artifact contains actual dashboard/WSL screenshots and a startup log. It is intentionally excluded from downloadable release assets. Skia rendering must appear in the log or the UI smoke test fails.

## Manual acceptance matrix

The automated GUI run is on a Windows x64 hosted runner. These remain hardware/OS acceptance checks; CI success must not be described as testing them:

| Check | Expected behavior |
|---|---|
| Windows 10 1809 and Windows 11 | Native startup, usable controls and fallbacks |
| Physical x86 and ARM64 | Native Skia library loads; no emulation-only assumption |
| 100%, 150%, 200% DPI; mixed-DPI monitors | Chart redraws sharply, forms and action labels remain reachable |
| 200% system text size | Translated labels wrap, inputs and confirmations remain usable |
| Windows High Contrast; Narrator | System control colors, readable chart summary, named icon buttons |
| Disable animations in Windows | Page changes suppress transition animation |
| Resize while WSL setup/error banners are open | All setup buttons and tabs remain reachable by scrolling |
| Modal destructive-operation confirmations | Theme matches; cancel is safe and background back shortcut is ignored |
| Restart after selecting theme/language | Saved preferences restored |

## Release procedure

1. Update the project, package manifest, installer fallback, About resources, README links, and changelog to the same version.
2. Run the Actions workflow on the candidate branch; review test logs, screenshots, dependency audit, and all six package builds.
3. Integrate the reviewed candidate into `main`, then push the matching `v<version>` tag. The workflow rejects tag/project mismatch.
4. Wait for the tag workflow to pass and publish three portable ZIPs, three installers, and `SHA256SUMS.txt`.
5. Verify release asset names, non-zero sizes, tag commit, and downloaded SHA-256 checksums. Never retarget an already published version tag.

Rollback: install the preceding published version. UI preferences are separate from firewall/network/WSL configuration; reverting the application does not undo operating-system changes made through its tools.
