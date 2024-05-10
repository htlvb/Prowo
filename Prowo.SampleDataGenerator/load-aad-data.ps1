Push-Location $PSScriptRoot

Connect-MgGraph

$AttendeeGroupId = (Get-MgGroup -Filter "displayName eq 'GrpSchueler'").Id
Get-MgGroupMemberAsUser -GroupId $AttendeeGroupId -All -Property Id,GivenName,Surname,Department,UserPrincipalName `
    | Select-Object Id,GivenName,Surname,Department,UserPrincipalName `
    | Where-Object { $_.GivenName -and $_.Surname -and $_.Department -and $_.UserPrincipalName } `
    | ConvertTo-Json > .\AttendeeCandidates.json

$OrganizerGroupId = (Get-MgGroup -Filter "displayName eq 'GrpLehrer'").Id
Get-MgGroupMemberAsUser -GroupId $OrganizerGroupId -All -Property Id,GivenName,Surname,UserPrincipalName `
    | Select-Object Id,GivenName,Surname,UserPrincipalName `
    | ConvertTo-Json > .\OrganizerCandidates.json

Pop-Location
