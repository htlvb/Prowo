$Schema = Get-Content db-schema.sql | Out-String
$Schema | docker exec -i prowo-db-1 /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "Test123$" -d model
