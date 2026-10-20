; DropMorph installer (Inno Setup 7). Build it with build/build-installer.ps1, which publishes the app, generates
; obj\engines.iss (build/tools/make-installer-engines.cs) and runs
;   ISCC.exe /DAppVersion=1.2.0 /DPublishDir=<published app> /O<output folder> installer\DropMorph.iss
;
; Besides copying files it
;  - installs for all users into Program Files (administrator rights, one UAC prompt). DropMorph itself keeps running as
;    the signed-in user without elevation: Windows blocks drag and drop from Explorer into elevated windows;
;  - closes a running DropMorph first ("DropMorph.exe --quit") and moves DropMorphExplorerMenu.dll aside when Explorer has it
;    loaded, so updates need no restart;
;  - takes over an installation of DropMorph 1.1 or earlier (Velopack, per user in %LocalAppData%\DropMorph): removes it but
;    keeps the settings (%APPDATA%\DropMorph) and downloaded engines (%LocalAppData%\DropMorph\engines);
;  - on Windows 11 registers the top-level File Explorer menu entry (an unsigned sparse package, which needs the
;    administrator rights Setup already has);
;  - offers the optional conversion engines; DropMorph downloads the chosen ones (verified) when Setup starts it;
;  - can put the "dropmorph" command line on PATH ({app}\cli\dropmorph.cmd runs dropmorph-cli.exe).
; The uninstaller runs "DropMorph.exe --uninstall-cleanup" for the per-user parts (UninstallCleanup.cs).

#ifndef AppVersion
  #error AppVersion is not defined: build with build/build-installer.ps1
#endif
#ifndef PublishDir
  #error PublishDir is not defined: build with build/build-installer.ps1
#endif

#define AppName "DropMorph"
#define AppExe "DropMorph.exe"
#define RepositoryUrl "https://github.com/cod4nitish/DropMorph"
; Never change: Windows finds the installed DropMorph by this id (uninstall key "{<id>}_is1").
#define AppGuid "C1381E52-0B3C-47AB-A119-980B1A1F5E69"

[Setup]
AppId={{{#AppGuid}}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=Nitish Singh
AppPublisherURL={#RepositoryUrl}
AppSupportURL={#RepositoryUrl}/issues
AppUpdatesURL={#RepositoryUrl}/releases
AppCopyright=Copyright (c) DropMorph contributors
VersionInfoVersion={#AppVersion}
VersionInfoProductName={#AppName}
SetupArchitecture=x64
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
PrivilegesRequired=admin
DefaultDirName={autopf}\{#AppName}
DisableWelcomePage=no
DisableProgramGroupPage=yes
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\{#AppExe}
SetupIconFile=..\src\DropMorph.App\Assets\Icons\dropmorph.ico
WizardStyle=modern dynamic
WizardImageFile=wizard-light.png
WizardImageFileDynamicDark=wizard-dark.png
WizardSmallImageFile=..\src\DropMorph.App\Assets\Icons\dropmorph.png
OutputBaseFilename=DropMorph-{#AppVersion}-win-Setup
Compression=lzma2/ultra64
SolidCompression=yes
; DropMorph is closed by PrepareToInstall; Restart Manager is only the fallback, and only for DropMorph.exe: with DLLs in the
; filter it would offer to close Explorer, which loads DropMorphExplorerMenu.dll.
CloseApplications=yes
CloseApplicationsFilter=*.exe
RestartApplications=no
; Per-user parts (the old Velopack copy, Start menu shortcuts) belong to the account that runs Setup.
UsedUserAreasWarning=no
; The "dropmorph" command folder is added to PATH: tell running programs (new terminals) about it.
ChangesEnvironment=yes
SetupLogging=yes

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "ko"; MessagesFile: "compiler:Languages\Korean.isl"
Name: "zhcn"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

; Texts shared with the app (engine names, the two options of the General page) are generated into obj\engines.iss.
[CustomMessages]
en.GroupOptions=Options:
ko.GroupOptions=옵션:
zhcn.GroupOptions=选项：
en.TaskModernMenu=Also show it at the top of the Windows 11 menu, not only under “Show more options”
ko.TaskModernMenu=Windows 11 기본 메뉴에도 표시 (“추가 옵션 표시”를 누르지 않아도 보여요)
zhcn.TaskModernMenu=也显示在 Windows 11 主菜单中，而不仅是“显示更多选项”里
en.StatusRemovingOld=Removing the previous version (settings and engines are kept)...
ko.StatusRemovingOld=이전 버전을 정리하는 중 (설정과 엔진은 그대로 둬요)...
zhcn.StatusRemovingOld=正在移除旧版本（保留设置和引擎）...
en.StatusExplorerMenu=Adding DropMorph to the File Explorer menu...
ko.StatusExplorerMenu=탐색기 메뉴에 DropMorph를 추가하는 중...
zhcn.StatusExplorerMenu=正在将 DropMorph 添加到资源管理器菜单...
en.ExplorerMenuFailed=Windows did not add DropMorph to the top of the File Explorer menu:%n%n%1%n%nDropMorph still appears under “Show more options”. You can try again in DropMorph under Settings → General.
ko.ExplorerMenuFailed=Windows가 탐색기 기본 메뉴에 DropMorph를 추가하지 못했어요:%n%n%1%n%nDropMorph는 “추가 옵션 표시” 안에는 계속 나타나요. DropMorph의 설정 → 일반에서 다시 시도할 수 있어요.
zhcn.ExplorerMenuFailed=Windows 未能将 DropMorph 添加到资源管理器主菜单：%n%n%1%n%nDropMorph 仍会出现在“显示更多选项”中。你可以在 DropMorph 的 设置 → 常规 中重试。
en.ReadyEngines=Conversion engines (DropMorph downloads them when it starts):
ko.ReadyEngines=변환 엔진 (DropMorph가 시작하면서 내려받아요):
zhcn.ReadyEngines=转换引擎（DropMorph 启动后下载）：
en.FinishedRunning=DropMorph is installed and running in the notification area of the taskbar.%n%nTo convert files, drag them while holding the trigger key, or right-click them in File Explorer.
ko.FinishedRunning=DropMorph가 설치되어 작업 표시줄 알림 영역에서 실행 중이에요.%n%n파일을 변환하려면 지정한 키를 누른 채 파일을 끌거나, 탐색기에서 파일을 우클릭하세요.
zhcn.FinishedRunning=DropMorph 已安装，正在任务栏通知区域运行。%n%n要转换文件，请按住触发键拖动文件，或在资源管理器中右键单击文件。
en.TaskCli=Add the "dropmorph" command to PATH (convert from the command line)
ko.TaskCli="dropmorph" 명령어를 PATH에 추가 (명령줄에서 변환)
zhcn.TaskCli=将 "dropmorph" 命令添加到 PATH（在命令行中转换）
en.FinishedEngines=The conversion engines you picked are downloading in the DropMorph window.
ko.FinishedEngines=선택한 변환 엔진은 DropMorph 창에서 내려받고 있어요.
zhcn.FinishedEngines=你选择的转换引擎正在 DropMorph 窗口中下载。

[Tasks]
Name: "contextmenu"; Description: "{cm:TaskContextMenu}"; GroupDescription: "{cm:GroupOptions}"; Check: IsFreshInstall
Name: "contextmenu\top"; Description: "{cm:TaskModernMenu}"; GroupDescription: "{cm:GroupOptions}"; Check: IsFreshInstall and IsExplorerMenuSupported
Name: "startup"; Description: "{cm:TaskStartup}"; GroupDescription: "{cm:GroupOptions}"; Check: IsFreshInstall
Name: "cli"; Description: "{cm:TaskCli}"; GroupDescription: "{cm:GroupOptions}"; Check: not IsCliOnPath
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[UninstallRun]
; Quits DropMorph, then removes the Explorer menu entries, the startup entry and downloaded engines. Settings stay.
Filename: "{app}\{#AppExe}"; Parameters: "--uninstall-cleanup"; Flags: runhidden waituntilterminated; RunOnceId: "DropMorphCleanup"

[Code]
const
  // ExplorerMenuPackage in src/DropMorph.Platform.Windows (InstallerScriptTests keeps these in sync).
  ExplorerMenuName = 'DropMorph.ExplorerMenu';
  ExplorerMenuFamily = 'DropMorph.ExplorerMenu_hsfbcx1hx2mgt';
  ExplorerMenuDll = 'DropMorphExplorerMenu.dll';
  ExplorerMenuPackageFile = 'DropMorphExplorerMenu.msix';
  ExplorerMenuMinimumBuild = 22000;
  // EngineInstaller marks every installed engine folder with this file.
  EngineMarker = '.dropmorph-component';
  // DropMorph 1.1 and earlier: Velopack installed it per user and registered it here.
  VelopackUninstallKey = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\DropMorph';
  ERROR_INSUFFICIENT_BUFFER = 122;

var
  // DropMorph 1.2 or later is installed already: this run is an update, which keeps the user's choices.
  WasInstalled: Boolean;
  HadVelopackInstall: Boolean;
  ExplorerMenuWasRegistered: Boolean;
  EnginePage: TInputOptionWizardPage;
  // Package id per entry of EnginePage; empty for packages that are installed already.
  EngineIds: TArrayOfString;

function GetPackagesByPackageFamily(PackageFamilyName: String; var Count: Cardinal; PackageFullNames: NativeInt;
  var BufferLength: Cardinal; Buffer: NativeInt): Integer;
  external 'GetPackagesByPackageFamily@kernel32.dll stdcall delayload';

function IsFreshInstall: Boolean;
begin
  Result := not WasInstalled;
end;

function IsExplorerMenuSupported: Boolean;
var
  Version: TWindowsVersion;
begin
  // Windows 11 has the new menu; Explorer must be x64 to load the DLL.
  GetWindowsVersionEx(Version);
  Result := (Version.Build >= ExplorerMenuMinimumBuild) and (ProcessorArchitecture = paX64);
end;

function IsExplorerMenuRegistered: Boolean;
var
  Count, Length: Cardinal;
begin
  Count := 0;
  Length := 0;
  try
    Result := (GetPackagesByPackageFamily(ExplorerMenuFamily, Count, 0, Length, 0) = ERROR_INSUFFICIENT_BUFFER) and (Count > 0);
  except
    Result := False;
  end;
end;

// A PowerShell single-quoted string. PowerShell also takes the typographic quotes as single quotes.
function PsQuote(const Value: String): String;
begin
  Result := Value;
  StringChangeEx(Result, '''', '''''', True);
  StringChangeEx(Result, #$2018, #$2018#$2018, True);
  StringChangeEx(Result, #$2019, #$2019#$2019, True);
  StringChangeEx(Result, #$201A, #$201A#$201A, True);
  StringChangeEx(Result, #$201B, #$201B#$201B, True);
  Result := '''' + Result + '''';
end;

// Runs Windows PowerShell (the Appx cmdlets live there) with the rights of Setup. The command must not contain
// double quotes; on failure Error gets the exception message without the "NOTE: For additional information" tail.
function RunPowerShell(const Command: String; var Error: String): Boolean;
var
  ErrorFile: String;
  Lines: TArrayOfString;
  I, ResultCode, Note: Integer;
begin
  ErrorFile := ExpandConstant('{tmp}\powershell-error.txt');
  DeleteFile(ErrorFile);
  Result := Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
    '-NoLogo -NoProfile -NonInteractive -Command "$ErrorActionPreference = ''Stop''; try { ' + Command + '; exit 0 } ' +
    'catch { [IO.File]::WriteAllText(' + PsQuote(ErrorFile) + ', $_.Exception.Message, [Text.Encoding]::UTF8); exit 1 }"',
    '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
  Error := '';
  if Result then
    Exit;
  if LoadStringsFromFile(ErrorFile, Lines) then
    for I := 0 to GetArrayLength(Lines) - 1 do
      if Trim(Lines[I]) <> '' then
        Error := Error + Trim(Lines[I]) + ' ';
  Note := Pos('NOTE: For additional information', Error);
  if Note > 0 then
    Error := Copy(Error, 1, Note - 1);
  Error := Trim(Error);
  if Error = '' then
    Error := Format('PowerShell exit code %d', [ResultCode]);
  Log('PowerShell failed: ' + Error);
end;

procedure UnregisterExplorerMenu;
var
  Error: String;
begin
  RunPowerShell('Get-AppxPackage -Name ' + PsQuote(ExplorerMenuName) + ' | Remove-AppxPackage', Error);
end;

// Registers the package next to DropMorph.exe with the install folder as its external location. A fresh install first
// removes any older registration, which may point to another folder.
procedure RegisterExplorerMenu(RemoveFirst: Boolean);
var
  Command, Error: String;
begin
  WizardForm.StatusLabel.Caption := CustomMessage('StatusExplorerMenu');
  Command := 'Add-AppxPackage -Path ' + PsQuote(ExpandConstant('{app}\') + ExplorerMenuPackageFile) +
    ' -ExternalLocation ' + PsQuote(ExpandConstant('{app}')) + ' -AllowUnsigned -ForceUpdateFromAnyVersion';
  if RemoveFirst then
    Command := 'Get-AppxPackage -Name ' + PsQuote(ExplorerMenuName) + ' | Remove-AppxPackage; ' + Command;
  if not RunPowerShell(Command, Error) and not WizardSilent then
    MsgBox(FmtMessage(CustomMessage('ExplorerMenuFailed'), [Error]), mbError, MB_OK);
end;

procedure UpdateExplorerMenu;
begin
  if not IsExplorerMenuSupported then
    Exit;
  if WasInstalled then
  begin
    // An update keeps what was chosen in DropMorph (Settings > General) and refreshes the registration.
    if ExplorerMenuWasRegistered then
      RegisterExplorerMenu(False);
  end
  else if WizardIsTaskSelected('contextmenu\top') then
    RegisterExplorerMenu(True)
  else if IsExplorerMenuRegistered then
    UnregisterExplorerMenu;
end;

// Explorer keeps DropMorphExplorerMenu.dll loaded after showing the menu. A loaded DLL cannot be replaced or deleted, but
// it can be renamed: move it into Folder (same drive) and let Windows delete it at the next restart.
procedure MoveLoadedDllAside(const Folder: String);
var
  Dll, Aside: String;
begin
  Dll := ExpandConstant('{app}\') + ExplorerMenuDll;
  if not FileExists(Dll) or DeleteFile(Dll) then
    Exit;
  Aside := AddBackslash(Folder) + ExplorerMenuDll + '.' + GetDateTimeString('yyyymmddhhnnss', #0, #0) + '.old';
  if RenameFile(Dll, Aside) then
    RestartReplace(Aside, '')
  else
    Log('Could not move the loaded ' + Dll + ' aside');
end;

procedure DeleteFileOrSchedule(const Path: String);
begin
  if FileExists(Path) and not DeleteFile(Path) then
    RestartReplace(Path, '');
end;

// Deletes a folder tree; what Windows keeps open (a DLL loaded by Explorer) goes at the next restart.
procedure DeleteTreeOrSchedule(const Dir: String);
var
  Find: TFindRec;
  Path: String;
begin
  if not DirExists(Dir) then
    Exit;
  if FindFirst(AddBackslash(Dir) + '*', Find) then
  try
    repeat
      if (Find.Name <> '.') and (Find.Name <> '..') then
      begin
        Path := AddBackslash(Dir) + Find.Name;
        // Never follow junctions or symbolic links out of the folder.
        if (Find.Attributes and FILE_ATTRIBUTE_REPARSE_POINT) <> 0 then
          RemoveDir(Path)
        else if (Find.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0 then
          DeleteTreeOrSchedule(Path)
        else
          DeleteFileOrSchedule(Path);
      end;
    until not FindNext(Find);
  finally
    FindClose(Find);
  end;
  if not RemoveDir(Dir) then
    RestartReplace(Dir, '');
end;

// Removes the Velopack installation of DropMorph 1.1 and earlier without running its uninstaller, which would also
// delete the downloaded engines. Its registry entries (context menu, startup) are rewritten by the new DropMorph.
procedure RemoveVelopackInstall;
var
  Root: String;
begin
  WizardForm.StatusLabel.Caption := CustomMessage('StatusRemovingOld');
  // Its Explorer menu entry points into the folder that is deleted next.
  if ExplorerMenuWasRegistered then
    UnregisterExplorerMenu;
  Root := ExpandConstant('{localappdata}\DropMorph');
  DeleteFileOrSchedule(Root + '\Update.exe');
  DeleteFileOrSchedule(Root + '\DropMorph.exe');
  DeleteTreeOrSchedule(Root + '\current');
  DeleteTreeOrSchedule(Root + '\packages');
  DeleteFile(ExpandConstant('{userprograms}\DropMorph.lnk'));
  DeleteFile(ExpandConstant('{userdesktop}\DropMorph.lnk'));
  RegDeleteKeyIncludingSubkeys(HKCU, VelopackUninstallKey);
end;

const
  EnvironmentKey = 'SYSTEM\CurrentControlSet\Control\Session Manager\Environment';

// The folder with dropmorph.cmd (the command line), which goes on the system PATH.
function CliFolder: String;
begin
  Result := ExpandConstant('{app}\cli');
end;

function PathHas(const Paths, Folder: String): Boolean;
begin
  Result := Pos(';' + Uppercase(RemoveBackslashUnlessRoot(Folder)) + ';', ';' + Uppercase(Paths) + ';') > 0;
end;

function IsCliOnPath: Boolean;
var
  Paths: String;
begin
  Result := RegQueryStringValue(HKLM, EnvironmentKey, 'Path', Paths) and PathHas(Paths, CliFolder);
end;

procedure AddCliToPath;
var
  Paths: String;
begin
  if not RegQueryStringValue(HKLM, EnvironmentKey, 'Path', Paths) or PathHas(Paths, CliFolder) then
    Exit;
  if (Paths <> '') and (Copy(Paths, Length(Paths), 1) <> ';') then
    Paths := Paths + ';';
  // REG_EXPAND_SZ like Windows' own value, so %SystemRoot% entries keep working.
  RegWriteExpandStringValue(HKLM, EnvironmentKey, 'Path', Paths + CliFolder);
end;

procedure RemoveCliFromPath;
var
  Paths, Folder: String;
  At: Integer;
begin
  if not RegQueryStringValue(HKLM, EnvironmentKey, 'Path', Paths) then
    Exit;
  Folder := ';' + Uppercase(CliFolder) + ';';
  Paths := ';' + Paths + ';';
  At := Pos(Folder, Uppercase(Paths));
  if At = 0 then
    Exit;
  Delete(Paths, At, Length(Folder) - 1);
  RegWriteExpandStringValue(HKLM, EnvironmentKey, 'Path', Copy(Paths, 2, Length(Paths) - 2));
end;

// True when every listed component has been downloaded already (EngineInstaller writes the marker last).
function IsEngineInstalled(const MarkerFolders: String): Boolean;
var
  Rest, Folder: String;
  Comma: Integer;
begin
  Result := True;
  Rest := MarkerFolders;
  while Rest <> '' do
  begin
    Comma := Pos(',', Rest);
    if Comma = 0 then
    begin
      Folder := Rest;
      Rest := '';
    end
    else
    begin
      Folder := Copy(Rest, 1, Comma - 1);
      Rest := Copy(Rest, Comma + 1, MaxInt);
    end;
    if not FileExists(ExpandConstant('{localappdata}\DropMorph\engines\') + Folder + '\' + EngineMarker) then
    begin
      Result := False;
      Exit;
    end;
  end;
end;

// Called by AddEngines (generated) for every package of Settings > Engines.
procedure AddEngine(const Id, MarkerFolders, DownloadSize, InstalledSize: String; Suggested: Boolean);
var
  Index: Integer;
  Installed: Boolean;
begin
  Installed := IsEngineInstalled(MarkerFolders);
  Index := EnginePage.Add(CustomMessage('Engine_' + Id));
  SetArrayLength(EngineIds, Index + 1);
  if Installed then
  begin
    EngineIds[Index] := '';
    EnginePage.CheckListBox.ItemSubItem[Index] := CustomMessage('EngineInstalled');
    EnginePage.CheckListBox.ItemEnabled[Index] := False;
  end
  else
  begin
    EngineIds[Index] := Id;
    EnginePage.CheckListBox.ItemSubItem[Index] := FmtMessage(CustomMessage('EngineSizes'), [DownloadSize, InstalledSize]);
    EnginePage.Values[Index] := Suggested;
  end;
end;

#include "obj\engines.iss"

function SelectedEngines: String;
var
  I: Integer;
begin
  Result := '';
  for I := 0 to GetArrayLength(EngineIds) - 1 do
    if (EngineIds[I] <> '') and EnginePage.Values[I] then
    begin
      if Result <> '' then
        Result := Result + ',';
      Result := Result + EngineIds[I];
    end;
end;

function OnOff(Value: Boolean): String;
begin
  if Value then
    Result := 'on'
  else
    Result := 'off';
end;

// Starts DropMorph as the signed-in user (not elevated) with the choices made in the wizard. An update starts it in
// the tray, as it was running before.
procedure StartDropMorph;
var
  Parameters: String;
  ResultCode: Integer;
begin
  if WasInstalled then
    Parameters := '--background'
  else
    Parameters := '--install-engines=' + SelectedEngines +
      ' --start-with-windows=' + OnOff(WizardIsTaskSelected('startup')) +
      ' --context-menu=' + OnOff(WizardIsTaskSelected('contextmenu'));
  if not ExecAsOriginalUser(ExpandConstant('{app}\{#AppExe}'), Parameters, '', SW_SHOWNORMAL, ewNoWait, ResultCode) then
    Log('Could not start DropMorph: ' + SysErrorMessage(ResultCode));
end;

function InitializeSetup: Boolean;
begin
  WasInstalled := RegKeyExists(HKA, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{' + '{#AppGuid}' + '}_is1');
  HadVelopackInstall := FileExists(ExpandConstant('{localappdata}\DropMorph\Update.exe'))
    and DirExists(ExpandConstant('{localappdata}\DropMorph\current'));
  ExplorerMenuWasRegistered := IsExplorerMenuSupported and IsExplorerMenuRegistered;
  // (A line must not start with "[" in [Code]: the compiler would take it for a section.)
  Log(Format('Update: %d, Velopack install: %d, Explorer menu registered: %d', [Ord(WasInstalled),
    Ord(HadVelopackInstall), Ord(ExplorerMenuWasRegistered)]));
  Result := True;
end;

procedure InitializeWizard;
begin
  EnginePage := CreateInputOptionPage(wpSelectTasks, CustomMessage('EnginesTitle'), CustomMessage('EnginesNote'),
    CustomMessage('EnginesDescription'), False, False);
  AddEngines;
end;

// True when at least one engine package is not installed yet.
function AnyEngineOffered: Boolean;
var
  I: Integer;
begin
  Result := False;
  for I := 0 to GetArrayLength(EngineIds) - 1 do
    if EngineIds[I] <> '' then
      Result := True;
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  // Updates keep the engines; Settings > Engines manages them from then on.
  Result := (PageID = EnginePage.ID) and (WasInstalled or not AnyEngineOffered);
end;

function UpdateReadyMemo(Space, NewLine, MemoUserInfoInfo, MemoDirInfo, MemoTypeInfo, MemoComponentsInfo,
  MemoGroupInfo, MemoTasksInfo: String): String;
var
  Engines: String;
  I: Integer;
begin
  Result := MemoDirInfo;
  if MemoTasksInfo <> '' then
    Result := Result + NewLine + NewLine + MemoTasksInfo;
  if WasInstalled then
    Exit;
  Engines := '';
  for I := 0 to GetArrayLength(EngineIds) - 1 do
    if (EngineIds[I] <> '') and EnginePage.Values[I] then
      Engines := Engines + Space + CustomMessage('Engine_' + EngineIds[I]) + NewLine;
  if Engines <> '' then
    Result := Result + NewLine + NewLine + CustomMessage('ReadyEngines') + NewLine + Engines;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  Result := '';
  // DropMorph 1.2 and later exit when asked (after saving); this waits until they have.
  ResultCode := 0;
  if WasInstalled and FileExists(ExpandConstant('{app}\{#AppExe}')) then
    Exec(ExpandConstant('{app}\{#AppExe}'), '--quit', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  // DropMorph 1.1 and earlier, or a DropMorph that did not answer: end it.
  if HadVelopackInstall or (ResultCode <> 0) then
    Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM {#AppExe} /FI "USERNAME eq ' + GetUserNameString + '"', '',
      SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  case CurStep of
    ssInstall:
      begin
        MoveLoadedDllAside(ExpandConstant('{app}'));
        if HadVelopackInstall then
          RemoveVelopackInstall;
      end;
    ssPostInstall:
      begin
        UpdateExplorerMenu;
        if WizardIsTaskSelected('cli') then
          AddCliToPath;
        StartDropMorph;
      end;
  end;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = wpFinished then
  begin
    WizardForm.FinishedLabel.Caption := CustomMessage('FinishedRunning');
    if not WasInstalled and (SelectedEngines <> '') then
      WizardForm.FinishedLabel.Caption := WizardForm.FinishedLabel.Caption + #13#10#13#10 + CustomMessage('FinishedEngines');
    WizardForm.AdjustLabelHeight(WizardForm.FinishedLabel);
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  case CurUninstallStep of
    // Before files are removed: out of the install folder, so that it can be deleted.
    usUninstall:
      MoveLoadedDllAside(ExpandConstant('{tmp}'));
    usPostUninstall:
    begin
      RemoveCliFromPath;
      if DirExists(ExpandConstant('{app}')) and not RemoveDir(ExpandConstant('{app}')) then
        RestartReplace(ExpandConstant('{app}'), '');
    end;
  end;
end;
