# Contributing

Thank you for helping improve Win-XinAi-De-Tools.

## Development Environment

- Windows 10 version 1809 or later.
- Visual Studio 2022 with the Windows App SDK workload.
- .NET 8 SDK.
- Administrator access for firewall integration tests and manual feature testing.

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
