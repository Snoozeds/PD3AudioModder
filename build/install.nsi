; NSIS Installer Script for PD3AudioModder
; Requires NSIS 3.1.1+, NSISdl

!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "FileFunc.nsh"
!include "x64.nsh"
!include "WinMessages.nsh"

; Application Details
!define APPNAME "PD3AudioModder"
!define COMPANYNAME "Snoozeds"
!define DESCRIPTION "PD3 Audio Modding Tool"
!define VERSIONMAJOR 1
!define VERSIONMINOR 0
!define VERSIONBUILD 0

Name "${APPNAME}"
OutFile "${APPNAME}_Installer.exe"
BrandingText "${APPNAME} Installer"
InstallDir "$PROGRAMFILES64\${APPNAME}"
RequestExecutionLevel admin

; MUI Settings
!define MUI_ABORTWARNING
!define MUI_ICON "src\assets\icons\audio.ico"  ; Application icon
!define MUI_WELCOMEFINISHPAGE_BITMAP "welcome.bmp" ; Welcome bitmap

!define LicenseFile "licenses.txt"

; Pages
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_LICENSE "${LicenseFile}"
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_LANGUAGE "English"

; Sections
Section "Main Application" SecMain
    SectionIn RO
    SetOutPath "$INSTDIR"
    
    ; Copy published files using relative path
    File /r "..\output\*"
    
    ; Create uninstaller
    WriteUninstaller "$INSTDIR\Uninstall.exe"
    
    ; Create shortcut in Start Menu
    CreateDirectory "$SMPROGRAMS\${APPNAME}"
    CreateShortcut "$SMPROGRAMS\${APPNAME}\${APPNAME}.lnk" "$INSTDIR\PD3AudioModder.exe"
    CreateShortcut "$SMPROGRAMS\${APPNAME}\Uninstall.lnk" "$INSTDIR\Uninstall.exe"
    
    ; Write registry information for Add/Remove Programs
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APPNAME}" "DisplayName" "${APPNAME}"
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APPNAME}" "UninstallString" "$\"$INSTDIR\Uninstall.exe$\""
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APPNAME}" "QuietUninstallString" "$\"$INSTDIR\Uninstall.exe$\" /S"
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APPNAME}" "InstallLocation" "$INSTDIR"
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APPNAME}" "DisplayIcon" "$INSTDIR\PD3AudioModder.exe"
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APPNAME}" "Publisher" "${COMPANYNAME}"
    WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APPNAME}" "DisplayVersion" "${VERSIONMAJOR}.${VERSIONMINOR}.${VERSIONBUILD}"
    WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APPNAME}" "NoModify" 1
    WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APPNAME}" "NoRepair" 1
SectionEnd

; Optional Dependencies Section
Section "Dependencies" SecDependencies

    ; Check and install FFmpeg
    DetailPrint "Checking FFmpeg installation..."
    ClearErrors
    nsExec::ExecToStack 'where ffmpeg'
    Pop $0
    ${If} $0 != 0
        MessageBox MB_YESNO "FFmpeg not found. Do you want to download and install it?" IDNO skipFFmpeg
        DetailPrint "Downloading FFmpeg..."
        NSISdl::download "https://github.com/BtbN/FFmpeg-Builds/releases/latest/ffmpeg-master-latest-win64-gpl.zip" "$TEMP\ffmpeg.zip"
        Pop $R0
        ${If} $R0 == "success"
            nsExec::Exec '"powershell -Command "Expand-Archive -Path \"$TEMP\ffmpeg.zip\" -DestinationPath \"$TEMP\ffmpeg\" -Force"'
            CopyFiles "$TEMP\ffmpeg\ffmpeg-master-latest-win64-gpl\bin\ffmpeg.exe" "$INSTDIR"
            CopyFiles "$TEMP\ffmpeg\ffmpeg-master-latest-win64-gpl\bin\ffprobe.exe" "$INSTDIR"
            WriteRegExpandStr HKLM "SYSTEM\CurrentControlSet\Control\Session Manager\Environment" "Path" "$INSTDIR;%PATH%"
        ${Else}
            MessageBox MB_OK "Failed to download FFmpeg: $R0"
        ${EndIf}
        skipFFmpeg:
    ${EndIf}

    ; Check and install VGMStream CLI
    DetailPrint "Checking VGMStream CLI installation..."

    IfFileExists "$WINDIR\Sysnative\vgmstream-cli.exe" vgmFound skipSystem32Check
    skipSystem32Check:
        Goto vgmFoundDone

    IfFileExists "$WINDIR\System32\vgmstream-cli.exe" vgmFound vgmNotFound
    vgmFound:
        DetailPrint "vgmstream-cli is already installed."
        Goto vgmFoundDone

    vgmNotFound:
        MessageBox MB_YESNO "vgmstream-cli not found. Do you want to download and install it?" IDNO skipVGMStream
        DetailPrint "Downloading vgmstream-cli..."
        NSISdl::download "https://github.com/vgmstream/vgmstream/releases/latest/download/vgmstream-win64.zip" "$TEMP\vgmstream.zip"
        Pop $R0
        ${If} $R0 == "success"
            nsExec::Exec '"powershell -Command "Expand-Archive -Path \"$TEMP\vgmstream.zip\" -DestinationPath \"$TEMP\vgmstream\" -Force"'
            CopyFiles /SILENT "$TEMP\vgmstream\vgmstream-cli.exe" "$INSTDIR"
            CopyFiles /SILENT "$TEMP\vgmstream\*.dll" "$INSTDIR"
        ${Else}
            MessageBox MB_OK "Failed to download VGMStream CLI: $R0"
        ${EndIf}
    vgmFoundDone:
    skipVGMStream:

    ; Check and install Repak
    DetailPrint "Checking Repak installation..."
    ClearErrors
    nsExec::ExecToStack 'where repak'
    Pop $0
    ${If} $0 != 0
        MessageBox MB_YESNO "Repak not found. Do you want to download and install it?" IDNO skipRepak
        DetailPrint "Downloading Repak..."
        NSISdl::download "https://github.com/trumank/repak/releases/latest/download/repak.exe" "$INSTDIR\repak.exe"
        Pop $R0
        ${If} $R0 == "success"
            WriteRegExpandStr HKLM "SYSTEM\CurrentControlSet\Control\Session Manager\Environment" "Path" "$INSTDIR;%PATH%"
        ${Else}
            MessageBox MB_OK "Failed to download Repak: $R0"
        ${EndIf}
        skipRepak:
    ${EndIf}
SectionEnd

; Uninstaller Section
Section "Uninstall"
    ; Remove files
    RMDir /r "$INSTDIR"
    
    ; Remove shortcuts
    Delete "$SMPROGRAMS\${APPNAME}\${APPNAME}.lnk"
    Delete "$SMPROGRAMS\${APPNAME}\Uninstall.lnk"
    RMDir "$SMPROGRAMS\${APPNAME}"
    
    ReadRegStr $R0 HKLM "SYSTEM\CurrentControlSet\Control\Session Manager\Environment" "Path"
    StrCpy $R0 "$R0;$INSTDIR"
    WriteRegExpandStr HKLM "SYSTEM\CurrentControlSet\Control\Session Manager\Environment" "Path" "$R0"

    DeleteRegKey HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APPNAME}"
SectionEnd

; Section Descriptions
!insertmacro MUI_FUNCTION_DESCRIPTION_BEGIN
    !insertmacro MUI_DESCRIPTION_TEXT ${SecMain} "Main application files for PD3AudioModder"
    !insertmacro MUI_DESCRIPTION_TEXT ${SecDependencies} "Install optional dependencies (FFmpeg, VGMStream CLI, Repak)"
!insertmacro MUI_FUNCTION_DESCRIPTION_END

; Pre-Install Checks
Function .onInit
    ; Check for 64-bit Windows
    ${If} ${RunningX64}
        SetRegView 64
    ${Else}
        MessageBox MB_OK|MB_ICONSTOP "This installer requires a 64-bit version of Windows."
        Abort
    ${EndIf}
FunctionEnd