$subscriptionName = "Pay-As-You-Go"
$resourceGroupName = "prowo-rg"
$accountName = "prowo-acdb"
$regionName = "eastus" # westeurope
$dbName = "ProjectsDB"
$containerName = "Project"
$logAnalyticsWorkspace = "prowo-law"
$containerAppEnvironment = "prowo-cae"
$containerAppName = "prowo-ca"
$serverAppName = "Prowo-Server"
$clientAppName = "Prowo-Client"
$subscriptionId = az account subscription list --query "[?displayName == '$subscriptionName'].subscriptionId | [0]" -o tsv
# TODO set $gitHubAccessToken
# TODO set $dockerHubPassword

az login
az account set --name $subscriptionName
az group create --name $resourceGroupName --location $regionName

### Create server app registration
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
$msGraphId = az ad sp list --query "[?appDisplayName=='Microsoft Graph'].appId | [0]" -o tsv
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
# az ad app update --id $serverApp.appId --set oauth2Permissions=@$serverAppApiScopes # TODO do manually since Microsoft Graph doesn't support setting

Remove-Item $serverAppAppRoles, $serverAppRequiredResourceAccesses, $serverAppApiScopes

$serverAppCredentials = az ad app credential reset --id $serverApp.appId --display-name Initial --years 2 --append | ConvertFrom-Json

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

### Create client app registration
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
        "https://localhost/authentication/login-callback"
    ]
}
"@ | Set-Content $clientAppSpaRedirectUris
az ad app update --id $clientApp.appId --set spa=@$clientAppSpaRedirectUris # TODO manually set correct redirect uris

Remove-Item $clientAppRequiredResourceAccesses, $clientAppSpaRedirectUris

### Give admin consent to server and client app permissions
Write-Host "!!! Login with admin account !!!"
az login --allow-no-subscriptions
az ad app permission admin-consent --id $serverApp.appId
az ad app permission admin-consent --id $clientApp.appId
az logout
az account set --name $subscriptionName

### Create CosmosDb
az cosmosdb create --name $accountName --enable-free-tier true --resource-group $resourceGroupName --subscription $subscriptionName --locations regionName=$regionName
az cosmosdb sql database create --name $dbName --account-name $accountName --resource-group $resourceGroupName
az cosmosdb sql container create --name $containerName --partition-key-path /id --max-throughput 1000 --account-name $accountname --resource-group $resourceGroupName --database-name $dbName
$cosmosDbConnectionString = az cosmosdb keys list --name $accountName --resource-group $resourceGroupName --type connection-strings --query connectionStrings[0].connectionString --output tsv

### Create Container App
az monitor log-analytics workspace create --workspace-name $logAnalyticsWorkspace --resource-group $resourceGroupName
$logAnalyticsWorkspaceClientId = az monitor log-analytics workspace show --query customerId -o tsv --workspace-name $logAnalyticsWorkspace --resource-group $resourceGroupName
$logAnalyticsWorkspaceClientSecret = az monitor log-analytics workspace get-shared-keys --query primarySharedKey -o tsv --workspace-name $logAnalyticsWorkspace --resource-group $resourceGroupName
az containerapp env create --name $containerAppEnvironment --logs-workspace-id $logAnalyticsWorkspaceClientId --logs-workspace-key $logAnalyticsWorkspaceClientSecret --location $regionName --resource-group $resourceGroupName
az containerapp create `
    --name $containerAppName `
    --target-port 80 `
    --ingress external `
    --secrets "cosmos-db-connection-string=$cosmosDbConnectionString" "aad-client-secret=$($serverAppCredentials.password)" `
    --env-vars "ConnectionStrings__CosmosDb=secretref:cosmos-db-connection-string" "AzureAd__ClientSecret=secretref:aad-client-secret" `
    --environment $containerAppEnvironment `
    --resource-group $resourceGroupName
$sp = az ad sp create-for-rbac `
    --name github-actions-sp `
    --role contributor `
    --scopes /subscriptions/$subscriptionId/resourceGroups/$resourceGroupName `
    --sdk-auth `
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

### Delete resources
az containerapp delete --name $containerAppName --resource-group $resourceGroupName --yes
az containerapp env delete --name $containerAppEnvironment --resource-group $resourceGroupName --yes
az monitor log-analytics workspace delete --workspace-name $logAnalyticsWorkspace --resource-group $resourceGroupName --yes
az cosmosdb sql container delete --name $containerName --account-name $accountname --resource-group $resourceGroupName --database-name $dbName --yes
az cosmosdb sql database delete --name $dbName --account-name $accountname --resource-group $resourceGroupName --yes
az cosmosdb delete --name $accountName --resource-group $resourceGroupName --yes
az group delete --name $resourceGroupName --yes
