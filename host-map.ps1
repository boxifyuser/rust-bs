# Serves C:\RustMaps so Rust clients can download the custom map.
$ErrorActionPreference = "Stop"
$port = 8080
$root = "C:\RustMaps"

if (-not (Test-Path $root)) {
    Write-Error "Pasta nao encontrada: $root"
    exit 1
}

# Prefer localhost binding first; fallback to + (needs admin/urlacl)
$listener = New-Object System.Net.HttpListener
$prefixes = @(
    "http://127.0.0.1:$port/",
    "http://localhost:$port/",
    "http://*:$port/"
)
foreach ($p in $prefixes) {
    try { $listener.Prefixes.Add($p) } catch { }
}

try {
    $listener.Start()
} catch {
    Write-Host "Falha ao abrir porta $port."
    Write-Host "Rode start-map-host.bat como Administrador uma vez."
    Write-Host $_
    exit 1
}

Write-Host "Map host ativo na porta $port"
Write-Host "URL do mapa: http://45.168.168.88:$port/MAPA_FINAL.map"
Write-Host "Pasta: $root"
Write-Host "Deixe esta janela aberta. Ctrl+C para parar."
Write-Host ""

while ($listener.IsListening) {
    $ctx = $null
    try {
        $ctx = $listener.GetContext()
    } catch {
        Start-Sleep -Milliseconds 200
        continue
    }

    $req = $ctx.Request
    $res = $ctx.Response

    try {
        $rel = [Uri]::UnescapeDataString($req.Url.AbsolutePath.TrimStart("/"))
        if ([string]::IsNullOrWhiteSpace($rel)) { $rel = "MAPA_FINAL.map" }

        $full = [System.IO.Path]::GetFullPath((Join-Path $root $rel))
        $rootFull = [System.IO.Path]::GetFullPath($root)
        if (-not $full.StartsWith($rootFull, [StringComparison]::OrdinalIgnoreCase)) {
            $res.StatusCode = 403
            continue
        }

        if (-not (Test-Path -LiteralPath $full -PathType Leaf)) {
            Write-Host "$(Get-Date -Format HH:mm:ss) 404 $($req.RemoteEndPoint) /$rel"
            $res.StatusCode = 404
            $bytes = [Text.Encoding]::UTF8.GetBytes("Not Found")
            $res.ContentLength64 = $bytes.Length
            $res.OutputStream.Write($bytes, 0, $bytes.Length)
            continue
        }

        $info = Get-Item -LiteralPath $full
        $res.StatusCode = 200
        $res.ContentType = "application/octet-stream"
        $res.ContentLength64 = $info.Length

        if ($req.HttpMethod -ne "HEAD") {
            $fs = [System.IO.File]::OpenRead($full)
            try {
                $fs.CopyTo($res.OutputStream)
            } finally {
                $fs.Dispose()
            }
        }

        Write-Host "$(Get-Date -Format HH:mm:ss) 200 $($req.RemoteEndPoint) $($req.HttpMethod) /$rel ($($info.Length) bytes)"
    } catch {
        Write-Host "$(Get-Date -Format HH:mm:ss) 500 $($req.RemoteEndPoint) - $_"
        try { $res.StatusCode = 500 } catch { }
    } finally {
        try { $res.OutputStream.Close() } catch { }
        try { $res.Close() } catch { }
    }
}
