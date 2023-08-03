$subscriptionName = "Pay-As-You-Go"
$resourceGroupName = "rg-prowo"
$regionName = "westeurope"
$dbServerName = "dbs-prowo"
$dbDatabaseName = "db-prowo"
$logAnalyticsWorkspace = "prowo-law"
$containerAppEnvironment = "prowo-cae"
$containerAppName = "prowo-ca"
$serverAppName = "Prowo-Server"
$clientAppName = "Prowo-Client"

# TODO set $gitHubAccessToken
# TODO set $dockerHubPassword

az extension add --upgrade -n account
az extension add --upgrade -n rdbms-connect --version 1.0.3 # error with newer versions, s. https://github.com/Azure/azure-cli/issues/25067
az extension add --upgrade -n containerapp

"=== Logging in and creating resource group"
az account set --name $subscriptionName
$subscriptionId = az account show --query id -o tsv
az group create --name $resourceGroupName --location $regionName -o none

"=== Creating server app registration"
$serverAppAppRoles = New-TemporaryFile
@"
[
  {
    "allowedMemberTypes": [ "User" ],
    "description": "All project editors can edit others' projects.",
    "displayName": "All project editors",
    "isEnabled": true,
    "value": "Project.Write.All"
  },
  {
    "allowedMemberTypes": [ "User" ],
    "description": "Project editors can create projects.",
    "displayName": "Project editors",
    "isEnabled": true,
    "value": "Project.Write"
  },
  {
    "allowedMemberTypes": [ "User" ],
    "description": "Report creators can print reports.",
    "displayName": "Report creators",
    "isEnabled": true,
    "value": "Report.Create"
  },
  {
    "allowedMemberTypes": [ "User" ],
    "description": "Project attendees can attend projects.",
    "displayName": "Project attendees",
    "isEnabled": true,
    "value": "Project.Attend"
  }
]
"@ | Set-Content $serverAppAppRoles
$msGraphId = az ad sp list --filter "displayname eq 'Microsoft Graph'" --query "[].appId" -o tsv
$serverAppRequiredResourceAccesses = New-TemporaryFile
@"
[{
    "resourceAppId": "$msGraphId",
    "resourceAccess": [
        {
            "id": "$(az ad sp show --id $msGraphId --query "oauth2PermissionScopes[?value=='GroupMember.Read.All'].id | [0]" -o tsv)",
            "type": "Scope"
        },
        {
            "id": "$(az ad sp show --id $msGraphId --query "oauth2PermissionScopes[?value=='User.Read.All'].id | [0]" -o tsv)",
            "type": "Scope"
        }
   ]
}]
"@ | Set-Content $serverAppRequiredResourceAccesses
$serverAppApiScopes = New-TemporaryFile
@"
[{
    "adminConsentDescription": "Allows the app to access server app API endpoints.",
    "adminConsentDisplayName": "Access API",
    "isEnabled": true,
    "type": "Admin",
    "userConsentDescription": null,
    "userConsentDisplayName": null,
    "value": "Api.Access"
}]
"@ | Set-Content $serverAppApiScopes
$serverApp = az ad app create --display-name $serverAppName `
    --sign-in-audience AzureADMyOrg `
    --app-roles @$serverAppAppRoles `
    --required-resource-accesses @$serverAppRequiredResourceAccesses `
    | ConvertFrom-Json
az ad app update --id $serverApp.appId --identifier-uris "api://$($serverApp.appId)"

Write-Warning "TODO do manually since Microsoft Graph doesn't support this setting (Expose an API -> Add a scope)"
# az ad app update --id $serverApp.appId --set oauth2Permissions=@$serverAppApiScopes

Remove-Item $serverAppAppRoles, $serverAppRequiredResourceAccesses, $serverAppApiScopes

$serverAppCredentials = az ad app credential reset --id $serverApp.appId --display-name Initial --years 2 --append | ConvertFrom-Json

az ad sp create --id $serverApp.appId -o none
$serverAppResourceId = az ad sp show --id $serverApp.appId --query "id" -o tsv
$appRoleAssigments = @(
    [PSCustomObject]@{PrincipalId = az ad group list --query "[?displayName=='GrpLehrer'].id | [0]" -o tsv; AppRoleName = "Report.Create"}
    [PSCustomObject]@{PrincipalId = az ad group list --query "[?displayName=='GrpLehrer'].id | [0]" -o tsv; AppRoleName = "Project.Write"}
    [PSCustomObject]@{PrincipalId = az ad group list --query "[?displayName=='GrpSchueler'].id | [0]" -o tsv; AppRoleName = "Project.Attend"}
    [PSCustomObject]@{PrincipalId = az ad user list --query "[?userPrincipalName=='HOED@htlvb.at'].id | [0]" -o tsv; AppRoleName = "Project.Write.All"}
    [PSCustomObject]@{PrincipalId = az ad user list --query "[?userPrincipalName=='NEUB@htlvb.at'].id | [0]" -o tsv; AppRoleName = "Project.Write.All"}
    [PSCustomObject]@{PrincipalId = az ad user list --query "[?userPrincipalName=='STAS@htlvb.at'].id | [0]" -o tsv; AppRoleName = "Project.Write.All"}
    [PSCustomObject]@{PrincipalId = az ad user list --query "[?userPrincipalName=='EGGJ@htlvb.at'].id | [0]" -o tsv; AppRoleName = "Project.Write.All"}
    [PSCustomObject]@{PrincipalId = az ad user list --query "[?userPrincipalName=='EGGJ@htlvb.at'].id | [0]" -o tsv; AppRoleName = "Project.Attend"}
)
foreach ($item in $appRoleAssigments) {
    $appRoleAssignment = New-TemporaryFile
@"
{
    "principalId": "$($item.PrincipalId)",
    "resourceId": "$serverAppResourceId",
    "appRoleId": "$(az ad app show --id $serverApp.appId --query "appRoles[?value=='$($item.AppRoleName)'].id | [0]" -o tsv)"
}
"@ | Set-Content $appRoleAssignment
    az rest --method POST --uri "https://graph.microsoft.com/v1.0/servicePrincipals/$serverAppResourceId/appRoleAssignedTo" --headers "Content-Type=application/json" --body @$appRoleAssignment
    Remove-Item $appRoleAssignment
}

"=== Creating client app registration"
$clientAppRequiredResourceAccesses = New-TemporaryFile
@"
[{
    "resourceAppId": "$($serverApp.appId)",
    "resourceAccess": [
        {
            "id": "$(az ad sp show --id $serverApp.appId --query "oauth2PermissionScopes[?value=='Api.Access'].id | [0]" -o tsv)",
            "type": "Scope"
        }
   ]
}]
"@ | Set-Content $clientAppRequiredResourceAccesses
$clientApp = az ad app create --display-name $clientAppName `
    --sign-in-audience AzureADMyOrg `
    --required-resource-accesses @$clientAppRequiredResourceAccesses `
    | ConvertFrom-Json
$clientAppSpaRedirectUris = New-TemporaryFile
@"
{
    "redirectUris": [
        "https://localhost/authentication/login-callback",
        "https://prowo.htlvb.at/authentication/login-callback",
        "https://prowo-ca.delightfulbeach-9d9e17e5.westeurope.azurecontainerapps.io/authentication/login-callback"
    ]
}
"@ | Set-Content $clientAppSpaRedirectUris
az ad app update --id $clientApp.appId --set spa=@$clientAppSpaRedirectUris

Remove-Item $clientAppRequiredResourceAccesses, $clientAppSpaRedirectUris

Write-Warning "TODO: Update ServerApiScope in appsettings.json to 'api://$($serverApp.appId)/Api.Access'"

"=== Giving admin consent to server and client app permissions"
"!!! Login with admin account !!!"
az login --use-device-code --allow-no-subscriptions -o none
az ad app permission admin-consent --id $serverApp.appId
az ad app permission admin-consent --id $clientApp.appId
az logout
az account set --name $subscriptionName

"=== Creating SQL DB"
$dbAdmin = az ad user show --id eggj@htlvb.at | ConvertFrom-Json
az sql server create --name $dbServerName --resource-group $resourceGroupName --location $regionName --enable-ad-only-auth --external-admin-principal-type User --external-admin-name $dbAdmin.userPrincipalName --external-admin-sid $dbAdmin.id
az sql server firewall-rule create --resource-group $resourceGroupName --name AllowAzureResources --server $dbServername --start-ip-address 0.0.0.0 --end-ip-address 0.0.0.0
az sql db create --resource-group $resourceGroupName --server $dbServerName --name $dbDatabaseName --edition GeneralPurpose --family Gen5 --compute-model Serverless --capacity 1 --max-size 2GB --backup-storage-redundancy Zone

$dbTemporaryFirewallRuleName = az sql server firewall-rule create --resource-group $resourceGroupName --name TempAllowAll --server $dbServername --start-ip-address 0.0.0.0 --end-ip-address 255.255.255.255 --query name -o tsv
$dbCmd = az sql db show-connection-string --server $dbServerName --name $dbDatabaseName --client sqlcmd --auth-type ADIntegrated -o tsv
$dbCmd = $dbCmd -replace "-N","-N true" # fix cmd line args
$accessToken = az account get-access-token --query accessToken -o tsv
$dbCmd = "$dbCmd -P $accessToken"
Invoke-Expression "$dbCmd -i db-schema.sql"
az sql server firewall-rule delete --resource-group $resourceGroupName --server $dbServername --name $dbTemporaryFirewallRuleName

$mssqlDbConnectionString = az sql db show-connection-string --server $dbServerName --name $dbDatabaseName --client ado.net --auth-type ADIntegrated -o tsv

"=== Creating Container App"
az monitor log-analytics workspace create --workspace-name $logAnalyticsWorkspace --resource-group $resourceGroupName
$logAnalyticsWorkspaceClientId = az monitor log-analytics workspace show --query customerId -o tsv --workspace-name $logAnalyticsWorkspace --resource-group $resourceGroupName
$logAnalyticsWorkspaceClientSecret = az monitor log-analytics workspace get-shared-keys --query primarySharedKey -o tsv --workspace-name $logAnalyticsWorkspace --resource-group $resourceGroupName
az containerapp env create --name $containerAppEnvironment --logs-workspace-id $logAnalyticsWorkspaceClientId --logs-workspace-key $logAnalyticsWorkspaceClientSecret --location $regionName --resource-group $resourceGroupName -o none
az containerapp create `
    --name $containerAppName `
    --target-port 80 `
    --ingress external `
    --secrets "mssql-db-connection-string=$mssqlDbConnectionString" "aad-client-secret=$($serverAppCredentials.password)" `
    --env-vars "ConnectionStrings__Mssql=secretref:mssql-db-connection-string" "AzureAd__ClientSecret=secretref:aad-client-secret" `
    --environment $containerAppEnvironment `
    --resource-group $resourceGroupName `
    -o none
$sp = az ad sp create-for-rbac `
    --name github-actions-sp `
    --role contributor `
    --scopes /subscriptions/$subscriptionId/resourceGroups/$resourceGroupName `
    | ConvertFrom-Json
az containerapp github-action add `
    --name $containerAppName `
    --repo-url https://github.com/htlvb/Prowo `
    --branch main `
    --image prowo `
    --registry-url docker.io `
    --registry-username johannesegger `
    --registry-password $dockerHubPassword `
    --service-principal-client-id $sp.clientId `
    --service-principal-client-secret $sp.clientSecret `
    --service-principal-tenant-id $sp.tenantId `
    --token $gitHubAccessToken `
    --resource-group $resourceGroupName

<#
"=== Deleting resources"
az ad app delete --id (az ad app list --filter "displayName eq '$serverAppName'" --query "[].id" -o tsv)
az ad app delete --id (az ad app list --filter "displayName eq '$clientAppName'" --query "[].id" -o tsv)
az containerapp delete --name $containerAppName --resource-group $resourceGroupName --yes
az containerapp env delete --name $containerAppEnvironment --resource-group $resourceGroupName --yes
az monitor log-analytics workspace delete --workspace-name $logAnalyticsWorkspace --resource-group $resourceGroupName --yes
az sql server delete --resource-group $resourceGroupName --name $dbServerName --yes
az group delete --name $resourceGroupName --yes
#>
