# Installation and shortcuts / 安装与快捷方式

## Installer / 安装版

From v1.10.1, the installer has two independent, default-selected shortcut options:
从 v1.10.1 起，安装向导提供两个独立选项，默认均勾选：

| English | 中文 | Result / 结果 |
|---|---|---|
| Create a desktop shortcut | 创建桌面快捷方式 | Desktop `Win-XinAi-De-Tools` icon / 桌面同名图标 |
| Create Start Menu shortcuts | 创建开始菜单快捷方式 | Start > Win-XinAi-De-Tools folder, with app and uninstall entries / 开始菜单同名文件夹，包含软件与卸载入口 |

The options appear before installing files. The separate **Launch Win-XinAi-De-Tools** checkbox on the final page only controls immediate launch; unchecking it does not prevent shortcut creation. The installed app requests administrator approval when launched, as required for system configuration.

快捷方式选项位于复制文件之前。完成页的**启动 Win-XinAi-De-Tools**只控制是否立即启动；取消该选项不会影响快捷方式。通过快捷方式启动后，软件仍会按系统配置功能的需要请求管理员权限。

This is an administrator, all-users installation; shortcuts are created in the shared Windows desktop and Start Menu locations. Start Menu search can take time to refresh; the same-named folder in the app list is the direct entry. App shortcuts use the executable's icon and installation directory as their working directory.

安装需要管理员权限，快捷方式位于 Windows 公共桌面与公共开始菜单，供本机用户使用。开始菜单搜索索引可能需要时间刷新，可先从应用列表打开同名文件夹。软件快捷方式使用程序内置图标，并将工作目录设为安装目录。

## Reinstall and recovery / 重新安装与找回入口

- Reinstalling with the same installer identity displays both default-selected choices again rather than silently inheriting old task selections. Your new choices replace this application's old shortcuts; unrelated shortcuts are not deleted.
- Unchecking both options intentionally creates no app shortcut. Open `Win-XinAi-De-Tools.exe` in your selected installation directory, or rerun the installer and select either option. The default directory is `Win-XinAi-De-Tools` under Program Files (Program Files (x86) for a 32-bit package on 64-bit Windows).
- The previous flat Start Menu shortcut is migrated to the new folder, without a duplicate entry. Uninstall removes shortcuts created by the installer; Windows Installed apps / Programs and Features remains available even if Start Menu shortcuts were disabled.
- The portable ZIP only extracts files. Run its executable directly or create a shortcut yourself; no installation wizard or automatic shortcut creation is performed.

- 重新安装时，两项会再次默认勾选，不会静默沿用之前取消勾选的状态。新选择会替换本软件的旧快捷方式，不删除其他软件的入口。
- 若主动取消两项，则不会创建软件快捷方式；可从选定安装目录打开 `Win-XinAi-De-Tools.exe`，或重新运行安装程序勾选快捷方式。默认安装到 Program Files 下的 `Win-XinAi-De-Tools`；64 位系统上的 32 位安装包使用 Program Files (x86)。
- 旧版开始菜单的单个入口迁移到同名文件夹，避免重复。卸载时清理安装程序创建的快捷方式；即使不创建开始菜单入口，仍可从 Windows“已安装的应用／程序和功能”卸载。
- ZIP 便携版只有解压后的文件，不会自动创建快捷方式；请直接运行程序或手动创建快捷方式。

## Verification / 验证

```powershell
python scripts/test-installer.py
# Elevated disposable Windows runner only; requires CI=true.
./scripts/installer-smoke.ps1 -Installer artifacts/installer/Win-XinAi-De-Tools-Setup-x64.exe
```

Source contracts check default task selection, task scoping, bilingual labels, immediate-launch independence and exact shortcut migration paths. Windows x64 CI installs the real package without launching it, checks default/none/desktop-only/Start-Menu-only/both selections, verifies .lnk targets/icons/working directories, launches the installed app through its Start Menu shortcut, checks reinstallation and then uninstalls. Logs are in the `installer-verification` Actions artifact.

源码契约测试检查默认勾选、快捷方式与选项关联、中英文标签、立即启动独立性及清理路径。Windows x64 CI 实际执行安装，检查默认、不创建、仅桌面、仅开始菜单、两者都创建，验证快捷方式目标／图标／工作目录，从开始菜单入口启动已安装程序，并验证重新安装与卸载。日志位于 Actions 的 `installer-verification` 产物。

Interactive wizard checkbox presentation, Start Menu search indexing, Windows 10/11 desktop policies and physical x86/ARM64 installations still require manual acceptance. Silent-install tests do not prove those behaviors.

安装向导的交互展示、开始菜单搜索索引、Windows 10/11 桌面策略，以及物理 x86/ARM64 安装仍需手工验收；静默安装测试不能代替这些验证。

## MyGo side-by-side edition / MyGo 并行安装

From v1.11.1, `Win-XinAi-De-Tools-MyGo` has its own AppId, Program Files directory, desktop icon and Start Menu folder. Both default-selected tasks and optional postinstall launch follow the same rules. It never migrates/deletes the WinUI shortcuts. UI preferences are independent; system configuration, network backups and audit logs are shared. Preserve the bundled `engine/` directory. See [MyGo differences](MYGO.md).

从 v1.11.1 起，MyGo 版使用独立安装身份、目录与同名桌面/开始菜单入口；两个快捷方式选项默认勾选，立即启动仍为独立选项。重装/卸载不删除原版快捷方式。两版可同时安装，但系统配置共用，请勿并发执行冲突操作；勿删除 MyGo 包内 `engine/`。

```powershell
./scripts/installer-smoke.ps1 -Installer artifacts/mygo-installer/Win-XinAi-De-Tools-MyGo-Setup-x64.exe -Edition MyGo
```

The same real x64 lifecycle checks write `mygo-installer-verification`. Source contracts check both editions' exact paths and identities. Interactive wizard, indexing and physical ARM64 remain manual checks.


## v1.12.1: Finish-page launch / 安装完成启动修复

Both editions now specify `runascurrentuser` for their optional `[Run]` entry. Inno Setup's default `postinstall` launch returns to the original, normally non-elevated user; `CreateProcess` then rejects a `requireAdministrator` executable with error 740. The explicit flag retains Setup's already-elevated token. This does not disable UAC or remove the application's administrator manifest. With alternate administrator credentials, immediate launch uses that administrator's profile; close and relaunch the shortcut from your normal desktop to use your own profile after approving UAC.

两版均修复完成页立即启动时的错误 740，保留管理员清单、UAC、可选立即启动，以及两个独立、默认勾选的快捷方式选项。取消立即启动仍可从桌面或开始菜单打开。若在 UAC 中输入了另一个管理员账户，立即启动将使用该管理员的配置目录；可关闭后从日常桌面快捷方式重新启动。

The installer smoke now drives the real non-silent English wizard through Next/Install/Finish and waits for the installed app's ready log and normal shutdown. Silent installation is still tested separately to verify it does not launch. This elevated runner check verifies the actual Finish path but does not simulate a real user's secure-desktop UAC prompt or organizational policy. Native Windows ARM64 MyGo validation is a separate release gate; physical-device checks remain distinct.
