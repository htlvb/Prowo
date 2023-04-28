# Run in Container Apps container
apt update
apt install certbot -y
certbot certonly --domain prowo.htlvb.at --webroot --webroot-path /app/wwwroot/ --email "it@htlvb.at" --no-eff-email --agree-tos
# TODO
# * cat privkey.pem and fullchain.pem and copy contents to local machine
# * run `openssl.exe pkcs12 -export -out lets-encrypt.pfx -inkey privkey.pem -in fullchain.pem -keypbe PBE-SHA1-3DES -certpbe PBE-SHA1-3DES -macalg SHA1`
#   * see https://github.com/microsoft/azure-container-apps/issues/511#issuecomment-1340654908
# * run `az containerapp ssl upload --certificate-file .\lets-encrypt.pfx --environment prowo-cae --hostname prowo.htlvb.at --certificate-name lets-encrypt -g rg-prowo -n prowo-ca`
