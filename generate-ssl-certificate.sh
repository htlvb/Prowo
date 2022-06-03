# Run in Container Apps container
apt update
apt install certbot -y
certbot certonly --domain prowo.htlvb.at --webroot --webroot-path /app/wwwroot/ --email "it@htlvb.at" --no-eff-email --agree-tos
# TODO
# * cat privkey.pem and fullchain.pem and copy contents to local machine
# * run `openssl.exe pkcs12 -export -out prowo.htlvb.at.pfx -inkey privkey.pem -in fullchain.pem`
# * import .pfx into Container Apps Environment and use at Container Apps -> Custom domain
