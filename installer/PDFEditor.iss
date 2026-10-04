; ============================================================
;  Bộ cài đặt PDF Editor (Inno Setup 6.5+). Xem docs/installer.md.
;  Thường được gọi từ build_windows.bat:
;    ISCC /DAppVersion=1.0.0 /DRid=win-x64 /DSourceDir=<thu muc publish> installer\PDFEditor.iss
; ============================================================

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#ifndef Rid
  #define Rid "win-x64"
#endif
#ifndef SourceDir
  #define SourceDir "..\dist\publish\" + Rid
#endif

#define AppName "PDF Editor"
#define AppExe "PDFEditor.exe"
#define AppPublisher "Nguyễn Thị Bích Lý"
#define ProgId "PDFEditor.Document"
#define CapabilitiesKey "Software\PDFEditor\Capabilities"

[Setup]
; AppId KHÔNG được đổi: đổi thì Windows coi là app khác, bản mới không cập nhật / gỡ được bản cũ.
AppId={{F9C05033-07F3-42A7-827C-31583772D15F}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
VersionInfoVersion={#AppVersion}
VersionInfoProductName={#AppName}
VersionInfoDescription={#AppName} Setup
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
; Hỏi cài cho riêng người dùng này (không cần admin) hay cho mọi người (Program Files, cần UAC).
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
MinVersion=10.0
#if Rid == "win-x64"
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
#elif Rid == "win-arm64"
ArchitecturesAllowed=arm64
ArchitecturesInstallIn64BitMode=arm64
#endif
OutputDir=..\dist
OutputBaseFilename=PDFEditor-Setup-{#AppVersion}-{#Rid}
SetupIconFile=..\PDFEditorApp\Resources\app.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
WizardStyle=modern
Compression=lzma2/ultra64
SolidCompression=yes
; App đang mở khi cập nhật / gỡ → đề nghị đóng.
CloseApplications=yes
RestartApplications=no
ChangesAssociations=yes
ShowLanguageDialog=auto

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "vi"; MessagesFile: "Languages\Vietnamese.isl"

[CustomMessages]
en.AssocGroup=File associations:
vi.AssocGroup=Liên kết file:
en.AssocPdf=Add {#AppName} to "Open with" for &PDF files
vi.AssocPdf=Thêm {#AppName} vào mục "Mở bằng" cho file &PDF
en.PdfDocument=PDF Document
vi.PdfDocument=Tài liệu PDF
en.AppDescription=View, edit, annotate and sign PDF files
vi.AppDescription=Xem, chỉnh sửa, nhận xét và ký file PDF

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "assocpdf"; Description: "{cm:AssocPdf}"; GroupDescription: "{cm:AssocGroup}"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"; Comment: "{cm:AppDescription}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Comment: "{cm:AppDescription}"; Tasks: desktopicon

[Registry]
; Win + R → "PDFEditor"
Root: HKA; Subkey: "Software\Microsoft\Windows\CurrentVersion\App Paths\{#AppExe}"; ValueType: string; ValueName: ""; ValueData: "{app}\{#AppExe}"; Flags: uninsdeletekey

; ProgID dùng cho .pdf
Root: HKA; Subkey: "Software\Classes\{#ProgId}"; ValueType: string; ValueName: ""; ValueData: "{cm:PdfDocument}"; Flags: uninsdeletekey; Tasks: assocpdf
Root: HKA; Subkey: "Software\Classes\{#ProgId}\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#AppExe},0"; Tasks: assocpdf
Root: HKA; Subkey: "Software\Classes\{#ProgId}\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExe}"" ""%1"""; Tasks: assocpdf
Root: HKA; Subkey: "Software\Classes\.pdf\OpenWithProgids"; ValueType: string; ValueName: "{#ProgId}"; ValueData: ""; Flags: uninsdeletevalue; Tasks: assocpdf

; Mục "Open with" theo tên exe
Root: HKA; Subkey: "Software\Classes\Applications\{#AppExe}"; ValueType: string; ValueName: "FriendlyAppName"; ValueData: "{#AppName}"; Flags: uninsdeletekey; Tasks: assocpdf
Root: HKA; Subkey: "Software\Classes\Applications\{#AppExe}\SupportedTypes"; ValueType: string; ValueName: ".pdf"; ValueData: ""; Tasks: assocpdf
Root: HKA; Subkey: "Software\Classes\Applications\{#AppExe}\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExe}"" ""%1"""; Tasks: assocpdf

; Hiện trong Settings → Default apps (người dùng tự chọn làm mặc định)
Root: HKA; Subkey: "Software\PDFEditor"; Flags: uninsdeletekey; Tasks: assocpdf
Root: HKA; Subkey: "{#CapabilitiesKey}"; ValueType: string; ValueName: "ApplicationName"; ValueData: "{#AppName}"; Tasks: assocpdf
Root: HKA; Subkey: "{#CapabilitiesKey}"; ValueType: string; ValueName: "ApplicationDescription"; ValueData: "{cm:AppDescription}"; Tasks: assocpdf
Root: HKA; Subkey: "{#CapabilitiesKey}\FileAssociations"; ValueType: string; ValueName: ".pdf"; ValueData: "{#ProgId}"; Tasks: assocpdf
Root: HKA; Subkey: "Software\RegisteredApplications"; ValueType: string; ValueName: "{#AppName}"; ValueData: "{#CapabilitiesKey}"; Flags: uninsdeletevalue; Tasks: assocpdf

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent
