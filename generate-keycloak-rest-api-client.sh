#!/bin/bash

dotnet tool restore
dotnet kiota generate \
  --language CSharp \
  --class-name KeycloakAdminApiClient \
  --namespace-name Keycloak.AdminApi \
  --openapi https://www.keycloak.org/docs-api/latest/rest-api/openapi.yaml \
  --output ./Keycloak.AdminApi
