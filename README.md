
This project requires Visual Studio 2019

Before you build the project once, you need to build the subfolder of vs-pty.net/dep/terminal
Via powershell:
Import-Module .\tools\OpenConsole.psm1
Set-MsBuildDevEnvironment
Invoke-OpenConsoleBuild /p:Platform=x86 /p:configuration="Release"
Invoke-OpenConsoleBuild /p:Platform=x64 /p:configuration="Release"