mkdir -p ./.tools
wget -O ./.tools/tailwindcss https://github.com/tailwindlabs/tailwindcss/releases/download/v3.2.1/tailwindcss-linux-x64
chmod +x ./.tools/tailwindcss

pushd ./Prowo.WebAsm/Client
dotnet tool restore
dotnet libman restore
popd
