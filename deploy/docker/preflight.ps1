# Preflight del stack de TRONOX SGDEA.
#
# La maquina de desarrollo corre ~30 contenedores de 8 stacks hermanos. Este script se ejecuta
# ANTES de "docker compose up" y ABORTA con mensaje claro si algo puede tumbar a un vecino.
#
# Uso:  .\preflight.ps1          (aborta al primer fallo)
#       .\preflight.ps1 -Full    (revisa todo y reporta el conjunto de fallos)
#
# Convencion del proyecto: solo ASCII en scripts.

[CmdletBinding()]
param([switch]$Full)

$ErrorActionPreference = 'Stop'
$fallos = New-Object System.Collections.Generic.List[string]

function Paso($texto) { Write-Host "  [..] $texto" -NoNewline }
function Ok($detalle)  { Write-Host "`r  [OK] $detalle                              " -ForegroundColor Green }
function Fallo($detalle) {
    Write-Host "`r  [!!] $detalle                              " -ForegroundColor Red
    $script:fallos.Add($detalle)
    if (-not $Full) { Resumen; exit 1 }
}

function Resumen {
    Write-Host ""
    if ($script:fallos.Count -eq 0) {
        Write-Host "PREFLIGHT OK - se puede levantar el stack." -ForegroundColor Green
        Write-Host "  docker compose --env-file .env up -d"
    } else {
        Write-Host "PREFLIGHT FALLIDO ($($script:fallos.Count)):" -ForegroundColor Red
        foreach ($f in $script:fallos) { Write-Host "  - $f" -ForegroundColor Red }
        Write-Host ""
        Write-Host "NO levantes el stack hasta resolverlo: podrias tumbar un contenedor hermano." -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "=== Preflight TRONOX ===" -ForegroundColor Cyan
Write-Host ""

# --- 1. Docker responde ---
Paso "Docker en ejecucion"
try {
    docker info --format '{{.ServerVersion}}' 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "docker info fallo" }
    Ok "Docker responde"
} catch {
    Fallo "Docker no responde. Abre Docker Desktop y reintenta."
}

# --- 2. El .env existe ---
$envPath = Join-Path $PSScriptRoot ".env"
Paso "Archivo .env"
if (-not (Test-Path $envPath)) {
    Fallo ".env no existe. Copialo desde .env.example y pon claves propias."
} else {
    Ok ".env encontrado"
}

# --- 3. Puertos del .env libres ---
$puertos = @{}
if (Test-Path $envPath) {
    foreach ($linea in Get-Content $envPath) {
        if ($linea -match '^\s*([A-Z_]*PORT)\s*=\s*(\d+)\s*$') { $puertos[$matches[1]] = [int]$matches[2] }
    }
}

# Puertos publicados por contenedores YA existentes (incluidos los detenidos).
$ocupadosPorDocker = @{}
$psOut = docker ps -a --format "{{.Names}}|{{.Ports}}" 2>$null
foreach ($linea in $psOut) {
    $parte = $linea -split '\|', 2
    if ($parte.Count -lt 2) { continue }
    foreach ($m in [regex]::Matches($parte[1], ':(\d+)->')) {
        $ocupadosPorDocker[[int]$m.Groups[1].Value] = $parte[0]
    }
}

$enEscucha = @{}
try {
    foreach ($c in (Get-NetTCPConnection -State Listen -ErrorAction SilentlyContinue)) {
        $enEscucha[[int]$c.LocalPort] = $true
    }
} catch { }

foreach ($nombre in ($puertos.Keys | Sort-Object)) {
    $p = $puertos[$nombre]
    Paso "Puerto $p ($nombre)"
    if ($ocupadosPorDocker.ContainsKey($p) -and $ocupadosPorDocker[$p] -notlike "tronox-*") {
        Fallo "Puerto $p ($nombre) lo publica el contenedor '$($ocupadosPorDocker[$p])'. Cambia $nombre en .env."
    } elseif ($enEscucha.ContainsKey($p) -and -not $ocupadosPorDocker.ContainsKey($p)) {
        $libre = $p + 1
        while ($enEscucha.ContainsKey($libre) -or $ocupadosPorDocker.ContainsKey($libre)) { $libre++ }
        Fallo "Puerto $p ($nombre) ocupado por un proceso del sistema. Siguiente libre: $libre."
    } else {
        Ok "Puerto $p ($nombre) libre"
    }
}

# --- 4. Sin restos de una corrida previa a medias ---
Paso "Contenedores tronox-* previos"
$previos = docker ps -a --filter "name=^tronox-" --format "{{.Names}} ({{.State}})" 2>$null
if ($previos) {
    Write-Host ""
    foreach ($c in $previos) { Write-Host "       $c" -ForegroundColor Yellow }
    Write-Host "  [OK] Hay contenedores tronox-* previos; 'compose up' los reutiliza." -ForegroundColor Yellow
    Write-Host "       Si vienen de una corrida a medias: docker compose down" -ForegroundColor Yellow
} else {
    Ok "Sin contenedores tronox-* previos"
}

# --- 5. Recursos minimos ---
Paso "Espacio en disco"
$unidad = (Get-Location).Drive
if ($unidad) {
    $libreGb = [math]::Round($unidad.Free / 1GB, 1)
    if ($libreGb -lt 5) { Fallo "Solo quedan $libreGb GB libres en $($unidad.Name): (minimo recomendado 5 GB)." }
    else { Ok "$libreGb GB libres en $($unidad.Name):" }
} else { Ok "No se pudo determinar la unidad; se omite" }

Paso "Memoria asignada a Docker"
try {
    $memBytes = [int64](docker info --format '{{.MemTotal}}' 2>$null)
    $memGb = [math]::Round($memBytes / 1GB, 1)
    if ($memGb -lt 2) { Fallo "Docker tiene $memGb GB de RAM (minimo recomendado 2 GB)." }
    else { Ok "$memGb GB de RAM en Docker" }
} catch { Ok "No se pudo leer la memoria; se omite" }

# --- 6. Linea base de vecinos, para comparar despues del up ---
$vecinos = @(docker ps --format "{{.Names}}" 2>$null | Where-Object { $_ -notlike "tronox-*" })
Write-Host ""
Write-Host "  Contenedores hermanos en ejecucion ahora: $($vecinos.Count)" -ForegroundColor Cyan
Write-Host "  Tras 'compose up' vuelve a contarlos: el numero NO debe bajar." -ForegroundColor Cyan

Resumen
if ($fallos.Count -gt 0) { exit 1 }
exit 0
