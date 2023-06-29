# Run in Container Apps container
apt update
apt install certbot curl -y
certbot certonly --domain prowo.htlvb.at --webroot --webroot-path /app/wwwroot/ --email "it@htlvb.at" --no-eff-email --agree-tos
pushd /etc/letsencrypt/live/prowo.htlvb.at
cat fullchain.pem privkey.pem > output.pem
curl -sL https://aka.ms/InstallAzureCLIDeb | bash
az extension add --name containerapp
az login
az containerapp ssl upload --certificate-file output.pem --environment prowo-cae --hostname prowo.htlvb.at --certificate-name lets-encrypt -g rg-prowo -n prowo-ca
popd
