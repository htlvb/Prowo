#!/bin/bash

KCADM='$KCADM'

read -p "Keycloak admin user name: " ADMIN_USER_NAME
read -r -s -p "Keycloak admin user password: " ADMIN_USER_PASSWORD
$KCADM config credentials --server http://localhost:8080 --realm master --user $ADMIN_USER_NAME --password "$ADMIN_USER_PASSWORD"

$KCADM create clients --target-realm htlvb \
    --set 'name=HTLVB Prowo' \
    --set clientId=prowo \
    --set enabled=true \
    --set rootUrl=https://prowo.htlvb.at/ \
    --set baseUrl=https://prowo.htlvb.at/ \
    --set "redirectUris=[\"https://localhost:7206/authentication/login-callback\", \"https://prowo.htlvb.at/authentication/login-callback\"]" \
    --set "webOrigins=[\"+\"]" \
    --set publicClient=true \
    --set frontchannelLogout=true \
    --set "attributes.\"frontchannel.logout.url\"=https://prowo.htlvb.at/" \
    --set "attributes.\"post.logout.redirect.uris\"=https://localhost:7206/authentication/logut-callback##https://prowo.htlvb.at/authentication/logut-callback" \
    --set "attributes.\"pkce.code.challenge.method\"=S256"
CLIENT_ID=`$KCADM get clients --target-realm htlvb --fields id,clientId | jq '.[] | select(.clientId == "prowo") | .id' --raw-output`

# Client must be added explicitely as audience because it is skipped by default (see https://github.com/keycloak/keycloak/issues/12415#issuecomment-1571690295)
# Prowo instead skips audience validation
# $KCADM create client-scopes --target-realm htlvb \
#     --set name=prowo-aud \
#     --set "description=Add prowo as audience because clients aren't added by default." \
#     --set protocol=openid-connect \
#     --set "attributes.\"display.on.consent.screen\"=false" \
#     --set "attributes.\"include.in.token.scope\"=false"
# CLIENT_SCOPE_ID=`$KCADM get client-scopes --target-realm htlvb | jq '.[] | select(.name == "prowo-aud") | .id' --raw-output`
# $KCADM create \
#     client-scopes/$CLIENT_SCOPE_ID/protocol-mappers/models \
#     --target-realm htlvb

$KCADM create clients/$CLIENT_ID/roles --target-realm htlvb --set name=all-projects-editor
$KCADM create clients/$CLIENT_ID/roles --target-realm htlvb --set name=project-creator
$KCADM create clients/$CLIENT_ID/roles --target-realm htlvb --set name=report-viewer
$KCADM create clients/$CLIENT_ID/roles --target-realm htlvb --set name=project-attendee

$KCADM add-roles --target-realm htlvb --gname Lehrer --cclientid realm-management --rolename view-users
$KCADM add-roles --target-realm htlvb --gname Schueler --cclientid realm-management --rolename view-users
$KCADM add-roles --target-realm htlvb --uusername eggj --cclientid prowo --rolename all-projects-editor
$KCADM add-roles --target-realm htlvb --uusername eggj --cclientid prowo --rolename project-attendee
$KCADM add-roles --target-realm htlvb --uusername hoed --cclientid prowo --rolename all-projects-editor
$KCADM add-roles --target-realm htlvb --uusername prai --cclientid prowo --rolename all-projects-editor
$KCADM add-roles --target-realm htlvb --gname Lehrer --cclientid prowo --rolename project-creator
$KCADM add-roles --target-realm htlvb --gname Lehrer --cclientid prowo --rolename report-viewer
$KCADM add-roles --target-realm htlvb --gname Schueler --cclientid prowo --rolename project-attendee
