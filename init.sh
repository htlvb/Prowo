mkdir -p ./.tools
wget -O ./.tools/tailwindcss https://github.com/tailwindlabs/tailwindcss/releases/download/v3.2.1/tailwindcss-linux-x64 || exit 1
chmod +x ./.tools/tailwindcss

pushd ./Prowo.WebAsm/Client
dotnet tool restore || exit 1
dotnet libman restore || exit 1
popd
