#ifndef SourceDir
  #error SourceDir is required
#endif
#ifndef BuildArch
  #error BuildArch is required
#endif
#ifndef AppVersion
  #define AppVersion "1.8.0"
#endif

[Setup]
AppId={{AD8E0A23-F4C1-4DA9-9F52-26E5A6D38DC7}
AppName=Win-XinAi-De-Tools
AppVersion={#AppVersion}
AppPublisher=Fengge Network (沨哥网络)
DefaultDirName={autopf}\Win-XinAi-De-Tools
DefaultGroupName=Win-XinAi-De-Tools
DisableProgramGroupPage=yes
OutputDir=..\artifacts\installer
OutputBaseFilename=Win-XinAi-De-Tools-Setup-{#BuildArch}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
UninstallDisplayIcon={app}\Win-XinAi-De-Tools.exe
SetupIconFile=..\Assets\Win-XinAi-De-Tools.ico

#if BuildArch == "x64"
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
#elif BuildArch == "arm64"
ArchitecturesAllowed=arm64
ArchitecturesInstallIn64BitMode=arm64
#endif

[Languages]
Name: "chinesesimplified"; MessagesFile: "{#SourcePath}\ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Win-XinAi-De-Tools"; Filename: "{app}\Win-XinAi-De-Tools.exe"
Name: "{autodesktop}\Win-XinAi-De-Tools"; Filename: "{app}\Win-XinAi-De-Tools.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut / 创建桌面快捷方式"; GroupDescription: "Additional icons / 附加图标"

[Run]
Filename: "{app}\Win-XinAi-De-Tools.exe"; Description: "Launch Win-XinAi-De-Tools / 启动 Win-XinAi-De-Tools"; Flags: nowait postinstall skipifsilent
