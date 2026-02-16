$pat = "85OMN8z9WLaX6VFIlbaD8hRiAWEs8Px0QrlEsRUh2SSEZUXHenvIJQQJ99CBACAAAAAAAAAAAAASAZDON2Jl"
$headers = @{ Authorization = "Basic " + [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes(":$pat")); "Content-Type" = "application/json-patch+json" }

# Delete US-66 (duplicate)
try {
    Invoke-RestMethod -Uri "https://dev.azure.com/my-credit-plan/Ado%20-%20Ai%20Agents/_apis/wit/workitems/66?api-version=7.1" -Method DELETE -Headers $headers | Out-Null
    Write-Host "DELETED US-66"
} catch {
    Write-Host "US-66 delete: $($_.Exception.Message)"
}

# Move US-67 back to New
try {
    $body = '[{"op":"add","path":"/fields/System.State","value":"New"}]'
    $result = Invoke-RestMethod -Uri "https://dev.azure.com/my-credit-plan/Ado%20-%20Ai%20Agents/_apis/wit/workitems/67?api-version=7.1" -Method PATCH -Headers $headers -Body ([System.Text.Encoding]::UTF8.GetBytes($body))
    Write-Host "US-67 now: $($result.fields.'System.State')"
} catch {
    Write-Host "US-67 update: $($_.Exception.Message)"
}
