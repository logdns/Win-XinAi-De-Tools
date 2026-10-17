#ifndef SourceDir
  #error SourceDir is required
#endif
#ifndef BuildArch
  #error BuildArch is required
#endif
#ifndef AppVersion
  #define AppVersion "1.12.1"
#endif

[Setup]
AppId={{ED82C612-79BA-4D1D-B2BD-5D6F9FA79E61}
AppName=Win-XinAi-De-Tools-MyGo
AppVersion={#AppVersion}
AppPublisher=Fengge Network (沨哥网络)
DefaultDirName={autopf}\Win-XinAi-De-Tools-MyGo
DefaultGroupName=Win-XinAi-De-Tools-MyGo
DisableProgramGroupPage=yes
UsePreviousTasks=no
OutputDir=..\artifacts\mygo-installer
OutputBaseFilename=Win-XinAi-De-Tools-MyGo-Setup-{#BuildArch}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
UninstallDisplayIcon={app}\Win-XinAi-De-Tools-MyGo.exe
SetupIconFile=..\Assets\Win-XinAi-De-Tools.ico

#if BuildArch == "x64"
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
#elif BuildArch == "arm64"
ArchitecturesAllowed=arm64
ArchitecturesInstallIn64BitMode=arm64
#else
  #error MyGo supports only x64 and arm64
#endif

[Languages]
Name: "chinesesimplified"; MessagesFile: "{#SourcePath}\ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[InstallDelete]
; Remove only our shortcuts so reinstalling applies the newly selected tasks.
Type: files; Name: "{autoprograms}\Win-XinAi-De-Tools-MyGo.lnk"
Type: files; Name: "{autoprograms}\Win-XinAi-De-Tools-MyGo\Win-XinAi-De-Tools-MyGo.lnk"
Type: files; Name: "{autoprograms}\Win-XinAi-De-Tools-MyGo\Uninstall Win-XinAi-De-Tools-MyGo.lnk"
Type: files; Name: "{autodesktop}\Win-XinAi-De-Tools-MyGo.lnk"

[Icons]
Name: "{autoprograms}\Win-XinAi-De-Tools-MyGo\Win-XinAi-De-Tools-MyGo"; Filename: "{app}\Win-XinAi-De-Tools-MyGo.exe"; WorkingDir: "{app}"; IconFilename: "{app}\Win-XinAi-De-Tools-MyGo.exe"; Tasks: startmenuicon
Name: "{autoprograms}\Win-XinAi-De-Tools-MyGo\Uninstall Win-XinAi-De-Tools-MyGo"; Filename: "{uninstallexe}"; WorkingDir: "{app}"; Tasks: startmenuicon
Name: "{autodesktop}\Win-XinAi-De-Tools-MyGo"; Filename: "{app}\Win-XinAi-De-Tools-MyGo.exe"; WorkingDir: "{app}"; IconFilename: "{app}\Win-XinAi-De-Tools-MyGo.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "{cm:DesktopShortcut}"; GroupDescription: "{cm:ShortcutGroup}"
Name: "startmenuicon"; Description: "{cm:StartMenuShortcut}"; GroupDescription: "{cm:ShortcutGroup}"

[CustomMessages]
english.ShortcutGroup=Shortcuts (available even if you do not launch the app now):
english.DesktopShortcut=Create a desktop shortcut
english.StartMenuShortcut=Create Start Menu shortcuts
english.LaunchApp=Launch Win-XinAi-De-Tools-MyGo
chinesesimplified.ShortcutGroup=快捷方式（安装后不立即运行也可使用）：
chinesesimplified.DesktopShortcut=创建桌面快捷方式
chinesesimplified.StartMenuShortcut=创建开始菜单快捷方式
chinesesimplified.LaunchApp=启动 Win-XinAi-De-Tools-MyGo

[Run]
; The application requires elevation; retain Setup's elevated token (avoids CreateProcess error 740).
Filename: "{app}\Win-XinAi-De-Tools-MyGo.exe"; WorkingDir: "{app}"; Description: "{cm:LaunchApp}"; Flags: nowait postinstall skipifsilent runascurrentuser
