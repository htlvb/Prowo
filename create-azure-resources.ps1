$subscriptionName = "Pay-As-You-Go"
$resourceGroupName = "prowo-rg"
$accountName = "prowo-acdb"
$regionName = "eastus" # westeurope
$dbName = "ProjectsDB"
$containerName = "Project"

az login
az cosmosdb create --name $accountName --enable-free-tier true --resource-group $resourceGroupName --subscription $subscriptionName --locations regionName=$regionName
az cosmosdb sql database create --name $dbName --account-name $accountName --resource-group $resourceGroupName
az cosmosdb sql container create --name $containerName --partition-key-path /id --max-throughput 1000 --account-name $accountname --resource-group $resourceGroupName --database-name $dbName

az cosmosdb sql container delete --name $containerName --account-name $accountname --resource-group $resourceGroupName --database-name $dbName --yes
az cosmosdb sql database delete --name $dbName --account-name $accountname --resource-group $resourceGroupName --yes
az cosmosdb delete --name $accountName --resource-group $resourceGroupName --yes
