mkdir .\.tools -Force | Out-Null
Invoke-WebRequest https://github.com/tailwindlabs/tailwindcss/releases/download/v3.2.1/tailwindcss-windows-x64.exe -OutFile .\.tools\tailwindcss.exe

Push-Location .\Prowo.WebAsm\Client
dotnet tool restore
dotnet libman restore
Pop-Location
