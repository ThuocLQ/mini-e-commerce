[CmdletBinding()]
param(
    [string]$GatewayBaseUrl = "http://api.localhost:5027",
    [string]$StorefrontBaseUrl = "http://localhost:5027"
)

$ErrorActionPreference = "Stop"
$GatewayBaseUrl = $GatewayBaseUrl.TrimEnd("/")
$StorefrontBaseUrl = $StorefrontBaseUrl.TrimEnd("/")
$username = "session-smoke-" + [Guid]::NewGuid().ToString("N").Substring(0, 12)
$password = "SessionSmoke!2026"
$email = "$username@example.test"

Invoke-RestMethod -Method Post -Uri "$GatewayBaseUrl/auth/register" -ContentType "application/json" -Body (@{
    username = $username
    email = $email
    password = $password
} | ConvertTo-Json -Compress) | Out-Null

Invoke-RestMethod -Method Post -Uri "$StorefrontBaseUrl/api/session" -Headers @{ Origin = $StorefrontBaseUrl } -ContentType "application/json" -Body (@{
    userName = $username
    password = $password
} | ConvertTo-Json -Compress) -SessionVariable storefrontSession | Out-Null

$token = ($storefrontSession.Cookies.GetCookies($StorefrontBaseUrl) | Where-Object Name -eq "microshop_access_token").Value
if ([string]::IsNullOrWhiteSpace($token)) {
    throw "Storefront did not issue an authenticated session cookie."
}

$sessionBeforeLogout = Invoke-RestMethod -Uri "$StorefrontBaseUrl/api/session" -WebSession $storefrontSession
if ($sessionBeforeLogout.user.userName -ne $username) {
    throw "Storefront session did not resolve the expected customer."
}

$logout = Invoke-RestMethod -Method Delete -Uri "$StorefrontBaseUrl/api/session" -Headers @{ Origin = $StorefrontBaseUrl } -WebSession $storefrontSession
if ($logout.success -ne $true) {
    throw "Storefront logout did not return success."
}

try {
    Invoke-RestMethod -Uri "$StorefrontBaseUrl/api/session" -WebSession $storefrontSession -ErrorAction Stop | Out-Null
    throw "Storefront session remained available after logout."
}
catch {
    $statusCode = [int]$_.Exception.Response.StatusCode
    if ($statusCode -ne 401) { throw }
}

$staleTokenStatus = & curl.exe -sS -o NUL -w "%{http_code}" -H "Authorization: Bearer $token" "$GatewayBaseUrl/auth/me"
if ($staleTokenStatus -ne "401") {
    throw "Gateway accepted a token revoked through the Storefront session flow. HTTP $staleTokenStatus."
}

Write-Host "[ok] Storefront logout revoked the BFF session and its previous gateway token."