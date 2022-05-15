mkdir .\.tools -Force | Out-Null
Invoke-WebRequest https://github.com/tailwindlabs/tailwindcss/releases/download/v3.0.24/tailwindcss-windows-x64.exe -OutFile .\.tools\tailwindcss.exe
