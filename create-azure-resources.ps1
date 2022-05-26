$subscriptionName = "Pay-As-You-Go"
$resourceGroupName = "prowo-rg"
$accountName = "prowo-acdb"
$regionName = "eastus" # westeurope
$dbName = "ProjectsDB"
$containerName = "Project"
$logAnalyticsWorkspace = "prowo-law"
$containerAppEnvironment = "prowo-cae"
$containerAppName = "prowo-ca"
$appServicePlanName = "prowo-asp"
$webAppName = "htlvb-prowo"
$deploymentRepository = "htlvb/prowo"
$subscriptionId = az account subscription list --query "[?displayName == '$subscriptionName'].subscriptionId | [0]" -o tsv
$tenantId = az account tenant list --query "[0].tenantId" -o tsv
$clientId = az ad app list --query "[?displayName == 'Prowo'].appId | [0]" -o tsv
# TODO set $clientSecret
# TODO set $gitHubAccessToken
# TODO set $dockerHubPassword

az login
az account set --name $subscriptionName
az group create --name $resourceGroupName --location $regionName
az cosmosdb create --name $accountName --enable-free-tier true --resource-group $resourceGroupName --subscription $subscriptionName --locations regionName=$regionName
az cosmosdb sql database create --name $dbName --account-name $accountName --resource-group $resourceGroupName
az cosmosdb sql container create --name $containerName --partition-key-path /id --max-throughput 1000 --account-name $accountname --resource-group $resourceGroupName --database-name $dbName
az appservice plan create --name $appServicePlanName --location $regionName --is-linux --sku FREE --resource-group $resourceGroupName
$cosmosDbConnectionString = az cosmosdb keys list --name $accountName --resource-group $resourceGroupName --type connection-strings --query connectionStrings[0].connectionString --output tsv

az monitor log-analytics workspace create --workspace-name $logAnalyticsWorkspace --resource-group $resourceGroupName
$logAnalyticsWorkspaceClientId = az monitor log-analytics workspace show --query customerId -o tsv --workspace-name $logAnalyticsWorkspace --resource-group $resourceGroupName
$logAnalyticsWorkspaceClientSecret = az monitor log-analytics workspace get-shared-keys --query primarySharedKey -o tsv --workspace-name $logAnalyticsWorkspace --resource-group $resourceGroupName
az containerapp env create --name $containerAppEnvironment --logs-workspace-id $logAnalyticsWorkspaceClientId --logs-workspace-key $logAnalyticsWorkspaceClientSecret --location $regionName --resource-group $resourceGroupName
az containerapp create `
    --name $containerAppName `
    --image docker.io/johannesegger/prowoweb:latest `
    --target-port 80 `
    --ingress external `
    --secrets "cosmos-db-connection-string=$cosmosDbConnectionString" "aad-client-secret=$clientSecret" `
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
    --service-principal-client-id $clientId `
    --service-principal-client-secret $clientSecret `
    --service-principal-tenant-id $tenantId `
    --token $gitHubAccessToken `
    --resource-group $resourceGroupName

az webapp create --name $webAppName --runtime DOTNETCORE:6.0 --plan $appServicePlanName --resource-group $resourceGroupName
az webapp config connection-string set --settings "CosmosDb=$cosmosDbConnectionString" --connection-string-type Custom --name $webAppName --resource-group $resourceGroupName
az webapp config appsettings set --settings "AzureAD__ClientSecret=$clientSecret" --name $webAppName --resource-group $resourceGroupName
az webapp deployment github-actions add --repo $deploymentRepository --branch main --token $gitHubAccessToken --name $webAppName --resource-group $resourceGroupName

az webapp delete --name $webAppName --resource-group $resourceGroupName
az appservice plan delete --name $appServicePlanName --resource-group $resourceGroupName
az cosmosdb sql container delete --name $containerName --account-name $accountname --resource-group $resourceGroupName --database-name $dbName --yes
az cosmosdb sql database delete --name $dbName --account-name $accountname --resource-group $resourceGroupName --yes
az cosmosdb delete --name $accountName --resource-group $resourceGroupName --yes
az group delete --name $resourceGroupName
