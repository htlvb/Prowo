Push-Location $PSScriptRoot

Connect-AzureAD

$attendeeGroupId = (Get-AzureADGroup -Filter "displayName eq 'GrpSchueler'").ObjectId
Get-AzureADGroupMember -ObjectId $attendeeGroupId -All $true `
    | Select-Object ObjectId,GivenName,Surname,Department `
    | ConvertTo-Json > .\AttendeeCandidates.json

$organizerGroupId = (Get-AzureADGroup -Filter "displayName eq 'GrpLehrer'").ObjectId
Get-AzureADGroupMember -ObjectId $organizerGroupId -All $true `
    | Select-Object ObjectId,GivenName,Surname,UserPrincipalName `
    | ConvertTo-Json > .\OrganizerCandidates.json

Pop-Location
