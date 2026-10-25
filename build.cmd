@echo off
rem 一键重编所有桌宠：只用系统自带 csc（.NET Framework 4），无需安装任何东西
rem 产出 pets/太对了/太对了.exe 和 pets/峰哥/峰哥.exe
setlocal

set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
  echo 找不到 csc.exe —— 需要 .NET Framework 4.x（Windows 10/11 自带）。& exit /b 1
)

set WPF=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\WPF
if not exist "%WPF%\PresentationFramework.dll" set WPF=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\WPF

set REFS=-r:"%WPF%\PresentationFramework.dll" -r:"%WPF%\PresentationCore.dll" -r:"%WPF%\WindowsBase.dll" -r:"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\System.Xaml.dll"

for %%D in ("太对了" "峰哥") do (
  echo === building pets\%%~D\%%~D.exe ===
  "%CSC%" -nologo -target:winexe -platform:anycpu -win32icon:"pets\%%~D\face.ico" -out:"pets\%%~D\%%~D.exe" %REFS% -r:System.Web.Extensions.dll src\Program.cs || exit /b 1
)
echo done.
