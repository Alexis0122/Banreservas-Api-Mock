param([string]$BaseUrl = "http://localhost:9464")

$oldTimeout = $env:Mock__TimeoutDelayMs
$env:Mock__TimeoutDelayMs = "100"
$server = Start-Process dotnet -ArgumentList "run --launch-profile http --no-build" -WorkingDirectory $PSScriptRoot -PassThru -WindowStyle Hidden

function Assert-Equal($Expected, $Actual, [string]$Name) {
    if ($Expected -ne $Actual) { throw "${Name}: esperado $Expected; recibido $Actual" }
}

function Status([scriptblock]$Call) {
    try { & $Call | Out-Null; return 200 }
    catch { return [int]$_.Exception.Response.StatusCode }
}

try {
    for ($i = 0; $i -lt 30; $i++) {
        try { Invoke-RestMethod "$BaseUrl/api/v1/auth" -Method Post -ContentType "application/json" -Headers @{ Usuario = "mock-usuario"; Llave = "mock-llave" } -Body '{"id_consumidor":"check"}' | Out-Null; break }
        catch { Start-Sleep -Milliseconds 200; if ($i -eq 29) { throw } }
    }

    $auth = Invoke-RestMethod "$BaseUrl/api/v1/auth" -Method Post -ContentType "application/json" -Headers @{ Usuario = "mock-usuario"; Llave = "mock-llave" } -Body '{"id_consumidor":"check"}'
    Assert-Equal "mock-access-token" $auth.acceso_token "token"
    Assert-Equal 200 (Status { Invoke-RestMethod "$BaseUrl/health" }) "health check"
    Assert-Equal 200 (Status { Invoke-RestMethod "$BaseUrl/swagger/index.html" }) "Swagger UI"
    $openApi = Invoke-RestMethod "$BaseUrl/swagger/v1/swagger.json"
    if (-not $openApi.paths.'/api/v1/auth' -or -not $openApi.paths.'/productos/v1/posicionconsolidada') { throw "Swagger no documenta los endpoints" }
    Assert-Equal "http" $openApi.components.securitySchemes.Bearer.type "seguridad OpenAPI"
    $body = '{"id_consumidor":"check","usuario":"tester","terminal":"127.0.0.1","fechaHora":"2026-09-09 10:11:09","version":1,"cliente":{"identificacion":{"numero":"40212546473","tipo":"Cedula"}}}'
    $ok = Invoke-RestMethod "$BaseUrl/productos/v1/posicionconsolidada" -Method Post -ContentType "application/json" -Headers @{ Authorization = "Bearer mock-access-token" } -Body $body
    Assert-Equal 6 $ok.cuentas.Count "cuentas"
    Assert-Equal 1 $ok.tarjetas.Count "tarjetas"
    Assert-Equal 11 $ok.prestamos.Count "prestamos"
    if ($ok.TRNID -notmatch '^\d{12}$') { throw "TRNID debe tener 12 dígitos" }

    $empty = Invoke-RestMethod "$BaseUrl/productos/v1/posicionconsolidada" -Method Post -ContentType "application/json" -Headers @{ Authorization = "Bearer mock-access-token" } -Body ($body -replace '40212546473', '00100000000')
    Assert-Equal 0 $empty.cuentas.Count "cliente sin productos"
    Assert-Equal "Cliente no posee productos" $empty.mensaje "mensaje vacío"
    $extended = Invoke-RestMethod "$BaseUrl/productos/v1/posicionconsolidada" -Method Post -ContentType "application/json" -Headers @{ Authorization = "Bearer mock-access-token" } -Body ($body -replace '40212546473', '00112345678')
    Assert-Equal 1 $extended.certificados.Count "certificados"
    Assert-Equal 1 $extended.tarjetasDebito.Count "tarjetas débito"
    $edge = Invoke-RestMethod "$BaseUrl/productos/v1/posicionconsolidada" -Method Post -ContentType "application/json" -Headers @{ Authorization = "Bearer mock-access-token" } -Body ($body -replace '40212546473', '00100000777')
    Assert-Equal "INACTIVA" $edge.cuentas[0].estado "cuenta de borde"
    Assert-Equal "100.00" $edge.prestamos[0].deudaVencida "préstamo vencido"

    Assert-Equal 401 (Status { Invoke-RestMethod "$BaseUrl/productos/v1/posicionconsolidada" -Method Post -ContentType "application/json" -Body $body }) "sin token"
    Assert-Equal 401 (Status { Invoke-RestMethod "$BaseUrl/productos/v1/posicionconsolidada" -Method Post -ContentType "application/json" -Headers @{ Authorization = "Bearer mock-expired-token" } -Body $body }) "token vencido"
    Assert-Equal 400 (Status { Invoke-RestMethod "$BaseUrl/productos/v1/posicionconsolidada" -Method Post -ContentType "application/json" -Headers @{ Authorization = "Bearer mock-access-token" } -Body ($body -replace '2026-09-09 10:11:09', '2026-02-30 10:11:09') }) "fecha inválida"
    Assert-Equal 404 (Status { Invoke-RestMethod "$BaseUrl/productos/v1/posicionconsolidada" -Method Post -ContentType "application/json" -Headers @{ Authorization = "Bearer mock-access-token" } -Body ($body -replace '40212546473', '00199999999') }) "cliente inexistente"
    Assert-Equal 500 (Status { Invoke-RestMethod "$BaseUrl/productos/v1/posicionconsolidada" -Method Post -ContentType "application/json" -Headers @{ Authorization = "Bearer mock-access-token" } -Body ($body -replace '40212546473', '00100000500') }) "error forzado"
    Assert-Equal 504 (Status { Invoke-RestMethod "$BaseUrl/productos/v1/posicionconsolidada" -Method Post -ContentType "application/json" -Headers @{ Authorization = "Bearer mock-access-token" } -Body ($body -replace '40212546473', '00100000504') }) "timeout forzado"
    $timer = [Diagnostics.Stopwatch]::StartNew()
    Invoke-RestMethod "$BaseUrl/productos/v1/posicionconsolidada" -Method Post -ContentType "application/json" -Headers @{ Authorization = "Bearer mock-access-token"; "X-Mock-Delay-Ms" = "150" } -Body $body | Out-Null
    $timer.Stop()
    if ($timer.ElapsedMilliseconds -lt 100) { throw "La demora configurada no se aplicó" }
    Write-Host "Verificación completada."
}
finally {
    $server | Stop-Process -ErrorAction SilentlyContinue
    $env:Mock__TimeoutDelayMs = $oldTimeout
}
