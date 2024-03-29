Push-Location $PSScriptRoot

Connect-AzureAD

$attendeeGroupId = (Get-AzureADGroup -Filter "displayName eq 'GrpSchueler'").ObjectId
Get-AzureADGroupMember -ObjectId $attendeeGroupId -All $true `
    | Select-Object ObjectId,GivenName,Surname,Department,UserPrincipalName `
    | Where-Object { $_.GivenName -and $_.Surname -and $_.Department -and $_.UserPrincipalName } `
    | ConvertTo-Json > .\AttendeeCandidates.json

$organizerGroupId = (Get-AzureADGroup -Filter "displayName eq 'GrpLehrer'").ObjectId
Get-AzureADGroupMember -ObjectId $organizerGroupId -All $true `
    | Select-Object ObjectId,GivenName,Surname,UserPrincipalName `
    | ConvertTo-Json > .\OrganizerCandidates.json

Pop-Location
