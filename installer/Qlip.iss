#define MyAppName "Qlip"
#define MyAppExeName "Qlip.exe"
#define MyAppPublisher "Qlip"

; Version can be injected by CI via env var (e.g. tag name)
#define EnvVersion GetEnv('QLIP_VERSION')
#if EnvVersion == ""
	#define MyAppVersion "1.0.0"
#else
	#define MyAppVersion EnvVersion
#endif

; GitHub Releases asset (zip) URL
; 想定: https://github.com/orarange/Qlip/releases/latest から
; latest/download/Qlip_win-x64.zip を配布する
#define MyAppPackageUrl "https://github.com/orarange/Qlip/releases/latest/download/Qlip_win-x64.zip"

[Setup]
AppId={{7A1A2C6D-5A2B-4B78-AF10-8F0C3A5A5B77}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=dist
OutputBaseFilename=QlipSetup_{#MyAppVersion}_win-x64
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#MyAppExeName}


; EULA / License acceptance
LicenseFile=EULA.txt
; Show third-party notices before install
InfoBeforeFile=THIRD-PARTY-NOTICES.txt

[Languages]
Name: "japanese"; MessagesFile: "compiler:Languages\\Japanese.isl"

[Tasks]
Name: "desktopicon"; Description: "デスクトップにアイコンを作成"; GroupDescription: "追加タスク"; Flags: unchecked

[Files]
; Webインストーラは中身を同梱しない（GitHubからダウンロード）
; ※ 将来、ダウンロード/展開用の追加ファイルが必要になった場合はここに追加

[Icons]
Name: "{group}\\{#MyAppName}"; Filename: "{app}\\{#MyAppExeName}"
Name: "{autodesktop}\\{#MyAppName}"; Filename: "{app}\\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\\{#MyAppExeName}"; Description: "{#MyAppName} を起動"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; [Files]で配布していないため、展開した実体は明示的に削除する
Type: filesandordirs; Name: "{app}"

[Code]
const
	PackageUrl = '{#MyAppPackageUrl}';
	PackageZipName = 'Qlip_win-x64.zip';

function QuoteReplace(const S: string): string; forward;

function GetSystemToolPath(const ToolExe: string): string;
var
	Candidate: string;
	PathEnv: string;
	Part: string;
	P: Integer;
begin
	Candidate := ExpandConstant('{sys}\\' + ToolExe);
	if FileExists(Candidate) then
	begin
		Result := Candidate;
		exit;
	end;

	PathEnv := GetEnv('PATH');
	while PathEnv <> '' do
	begin
		P := Pos(';', PathEnv);
		if P = 0 then
		begin
			Part := PathEnv;
			PathEnv := '';
		end
		else
		begin
			Part := Copy(PathEnv, 1, P - 1);
			PathEnv := Copy(PathEnv, P + 1, Length(PathEnv));
		end;

		Part := Trim(Part);
		if Part = '' then
			continue;

		Candidate := AddBackslash(Part) + ToolExe;
		if FileExists(Candidate) then
		begin
			Result := Candidate;
			exit;
		end;
	end;

	Result := '';
end;

function GetPowerShellPath(): string;
begin
	Result := ExpandConstant('{sys}\\WindowsPowerShell\\v1.0\\powershell.exe');
	if FileExists(Result) then exit;
	Result := '';
end;

procedure FailWithMessage(const Msg: string);
begin
	SuppressibleMsgBox(Msg, mbCriticalError, MB_OK, IDOK);
	Abort;
end;

procedure SetInstallStatus(const Msg: string);
begin
	WizardForm.StatusLabel.Caption := Msg;
	WizardForm.StatusLabel.Refresh;
end;

procedure EnsureDir(const Dir: string);
begin
	if not ForceDirectories(Dir) then
		FailWithMessage('インストール先フォルダを作成できません: ' + Dir);
end;

procedure DownloadPackageOrFail(const ZipPath: string);
var
	CurlPath, PsPath: string;
	ResultCode: Integer;
	Args: string;
begin
	if FileExists(ZipPath) then
		DeleteFile(ZipPath);

	SetInstallStatus('Qlip を GitHub からダウンロード中...');

	CurlPath := GetSystemToolPath('curl.exe');
	if CurlPath <> '' then
	begin
		// -L: redirect follow, --fail: HTTP error => non-zero
		Args := '-L --fail --retry 3 --retry-delay 2 -o ' +
			AddQuotes(ZipPath) + ' ' + AddQuotes(PackageUrl);
		if not Exec(CurlPath, Args, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
			FailWithMessage('ダウンロードに失敗しました (curl 実行失敗)。');
		if (ResultCode <> 0) or (not FileExists(ZipPath)) then
			FailWithMessage('ダウンロードに失敗しました (curl 終了コード: ' + IntToStr(ResultCode) + ')。');
		exit;
	end;

	// Fallback: PowerShell (ほとんどのWindowsに存在)
	PsPath := GetPowerShellPath();
	if PsPath = '' then
		FailWithMessage('ダウンロード手段が見つかりません (curl.exe / PowerShell)。');

	Args := '-NoProfile -ExecutionPolicy Bypass -Command ' +
		AddQuotes(
			'$ProgressPreference="SilentlyContinue"; ' +
			'Invoke-WebRequest -Uri ' + QuoteReplace(PackageUrl) +
			' -OutFile ' + QuoteReplace(ZipPath)
		);

	if not Exec(PsPath, Args, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
		FailWithMessage('ダウンロードに失敗しました (PowerShell 実行失敗)。');
	if (ResultCode <> 0) or (not FileExists(ZipPath)) then
		FailWithMessage('ダウンロードに失敗しました (PowerShell 終了コード: ' + IntToStr(ResultCode) + ')。');
end;

procedure ExtractZipOrFail(const ZipPath, DestDir: string);
var
	TarPath, PsPath: string;
	ResultCode: Integer;
	Args: string;
	ExePath: string;
begin
	SetInstallStatus('Qlip を展開中...');

	TarPath := GetSystemToolPath('tar.exe');
	if TarPath <> '' then
	begin
		// Windowsのbsdtarはzipも展開可能
		Args := '-xf ' + AddQuotes(ZipPath) + ' -C ' + AddQuotes(DestDir);
		if not Exec(TarPath, Args, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
			FailWithMessage('展開に失敗しました (tar 実行失敗)。');
		if ResultCode <> 0 then
			FailWithMessage('展開に失敗しました (tar 終了コード: ' + IntToStr(ResultCode) + ')。');
	end
	else
	begin
		// Fallback: PowerShell Expand-Archive
		PsPath := GetPowerShellPath();
		if PsPath = '' then
			FailWithMessage('展開手段が見つかりません (tar.exe / PowerShell)。');

		Args := '-NoProfile -ExecutionPolicy Bypass -Command ' +
			AddQuotes(
				'Expand-Archive -Force -LiteralPath ' + QuoteReplace(ZipPath) +
				' -DestinationPath ' + QuoteReplace(DestDir)
			);

		if not Exec(PsPath, Args, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
			FailWithMessage('展開に失敗しました (PowerShell 実行失敗)。');
		if ResultCode <> 0 then
			FailWithMessage('展開に失敗しました (PowerShell 終了コード: ' + IntToStr(ResultCode) + ')。');
	end;

	ExePath := AddBackslash(DestDir) + '{#MyAppExeName}';
	if not FileExists(ExpandConstant(ExePath)) then
		FailWithMessage('展開後に {#MyAppExeName} が見つかりません。配布zipの中身を確認してください。');
end;

function QuoteReplace(const S: string): string;
var
  T: string;
begin
  // PowerShellの文字列用に ' を '' にエスケープ
  T := S;
  StringChangeEx(T, '''', '''''', True);
  Result := '''' + T + '''';
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
	AppDir, ZipPath: string;
begin
	if CurStep = ssInstall then
	begin
		AppDir := ExpandConstant('{app}');
		ZipPath := ExpandConstant('{tmp}\\' + PackageZipName);

		EnsureDir(AppDir);
		DownloadPackageOrFail(ZipPath);
		ExtractZipOrFail(ZipPath, AppDir);

		SetInstallStatus('最終処理中...');
	end;
end;
