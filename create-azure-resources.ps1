$subscriptionName = "Pay-As-You-Go"
$resourceGroupName = "prowo-rg"
$accountName = "prowo-acdb"
$regionName = "eastus" # westeurope
$dbName = "ProjectsDB"
$containerName = "Project"
$appServicePlanName = "prowo-asp"
$webAppName = "htlvb-prowo"
$deploymentRepository = "htlvb/prowo"
# TODO set $clientSecret
# TODO set $gitHubAccessToken

az login
az account set --name $subscriptionName
az group create --name $resourceGroupName --location $regionName
az cosmosdb create --name $accountName --enable-free-tier true --resource-group $resourceGroupName --subscription $subscriptionName --locations regionName=$regionName
az cosmosdb sql database create --name $dbName --account-name $accountName --resource-group $resourceGroupName
az cosmosdb sql container create --name $containerName --partition-key-path /id --max-throughput 1000 --account-name $accountname --resource-group $resourceGroupName --database-name $dbName
az appservice plan create --name $appServicePlanName --location $regionName --is-linux --sku FREE --resource-group $resourceGroupName
$cosmosDbConnectionString = az cosmosdb keys list --name prowo-acdb --resource-group $resourceGroupName --type connection-strings --query connectionStrings[0].connectionString --output tsv
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
