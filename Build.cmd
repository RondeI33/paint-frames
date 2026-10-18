@echo off
setlocal
cd /d "%~dp0"
set "FRAMEWORK=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319"
if not exist "%FRAMEWORK%\csc.exe" set "FRAMEWORK=%WINDIR%\Microsoft.NET\Framework\v4.0.30319"
"%FRAMEWORK%\csc.exe" /nologo /target:winexe /optimize+ /out:PaintFrames.exe /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Xml.dll /r:"%FRAMEWORK%\WPF\UIAutomationClient.dll" /r:"%FRAMEWORK%\WPF\UIAutomationTypes.dll" /r:"%FRAMEWORK%\WPF\WindowsBase.dll" PaintFrames.cs AnimationTimeline.cs TrackEditor.cs
if errorlevel 1 (
    echo Build failed.
    pause
    exit /b 1
)
echo Built PaintFrames.exe. Double-click it to start.
pause
