# Contributing

Build and test on Windows. Keep user documentation focused on current behavior.

## Development Environment

- Windows 10 version 1809 or later.
- Visual Studio 2022 configured for .NET desktop and WinUI development.
- Windows SDK 10.0.19041.0 or newer.
- .NET 8 SDK.
- Administrator access for firewall integration tests and manual feature testing.

## Build and test

```powershell
dotnet publish Win-XinAi-De-Tools.csproj --configuration Release --runtime win-x64 --self-contained true -p:Platform=x64 -p:WindowsPackageType=None --output artifacts/portable/win-x64
dotnet test Win-XinAi-De-Tools.Tests/Win-XinAi-De-Tools.Tests.csproj --configuration Release
python scripts/audit-ui.py
python scripts/test-installer.py
```

For another architecture, use `win-x86` / `x86` or `win-arm64` / `ARM64`.
Installer changes also require the elevated, disposable Windows checks in [installation validation](docs/INSTALLATION.md#verification--验证), including the real non-silent Finish-page launch (not only silent installation). MyGo also runs on native Windows ARM64. Do not run installer smoke on a machine with an existing installation.
To enable actual firewall integration tests in an elevated Windows session:

```powershell
$env:PORTMANAGER_RUN_INTEGRATION = "1"
dotnet test Win-XinAi-De-Tools.Tests/Win-XinAi-De-Tools.Tests.csproj -c Release --filter "Category=WindowsIntegration"
Remove-Item Env:PORTMANAGER_RUN_INTEGRATION
```

The app calls `wsl.exe` directly. Optional bridge setup and build instructions are in [native/README.md](native/README.md).

Hyper-V tests generate scripts and use mocked cmdlets on Windows; they do not change real Hyper-V configuration. To run those contracts explicitly:

```powershell
dotnet test Win-XinAi-De-Tools.Tests/Win-XinAi-De-Tools.Tests.csproj -c Release --filter "Category=HyperVContract"
```

Actual feature/adapter/switch changes and reboot recovery need a disposable, locally accessible Windows machine with supported hardware. Follow [Hyper-V acceptance and recovery](docs/HYPER-V.md). Never run destructive networking checks over a remote session using the target adapter.

## Before Opening A Pull Request

1. Keep changes focused and preserve the existing WinUI 3 and Community Toolkit patterns.
2. Keep all user-facing strings in both `Localization/Strings.zh-CN.xaml` and `Localization/Strings.en-US.xaml`.
3. Add or update tests for service and model behavior.
4. Run `dotnet test Win-XinAi-De-Tools.Tests\Win-XinAi-De-Tools.Tests.csproj --configuration Release` on Windows.
5. If a native WSL helper changed, run `cargo test --locked --manifest-path native/wsl-helper-rust/Cargo.toml` and `go test ./...` from `native/wsl-helper-go`.
6. For UI changes, run `scripts/ui-smoke.ps1` against a published x64 build and review the screenshots; follow [UI validation](docs/UI-AND-VALIDATION.md) for DPI, accessibility, and other manual checks.
7. Run `git diff --check` and confirm that generated `bin/`, `obj/`, `target/`, and `artifacts/` files are not included.
8. Describe architecture-specific or administrator-permission requirements in the pull request.

## Reporting Bugs

Include the application version, Windows build, architecture, exact reproduction steps, and relevant logs from `%LOCALAPPDATA%\Win-XinAi-De-Tools\startup.log` or `%LOCALAPPDATA%\Win-XinAi-De-Tools\audit.log`. Remove private data before posting logs publicly.

## License

By contributing, you agree that your contributions are provided under the MIT License in this repository.

## MyGo edition

The native Go frontend and private shared .NET engine live in `mygo/` and `engine/`. Keep both edition versions synchronized. Use Go 1.27.2 or the pinned security-fixed version in the workflow; run `go test ./...`, `go vet ./...` and the pinned `govulncheck` command from `mygo/`. See [the complete build and capability boundaries](docs/MYGO.md). WinUI keeps x86/x64/ARM64; MyGo ships x64/ARM64. A release requires both matrices, both actual x64 installer/UI checks and native ARM64 MyGo execution/installation. Preserve the busy-close guard and verify initial window work-area fit and visible controls. New system-command output must have an explicit encoding contract; do not change global code pages to fix per-process text. Do not execute real network resets in CI; use parser and mocked-cmdlet contracts plus disposable local Windows acceptance.
