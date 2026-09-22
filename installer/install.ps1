# AI ERP - one-shot installer for Windows.
# Invoked by install.bat. Prepares a complete machine: PostgreSQL 16, AI ERP,
# AgentBridge + ErpTool, the company seed, and a PWA launcher.
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

# --- Constants -------------------------------------------------------------
$ErpRepo   = 'Graphene-Lab/AI-ERP'
$AbRepo    = 'Graphene-Lab/AgentBridge'
$ToolRepo  = 'Graphene-Lab/ErpTool'
$ErpTag    = if ($env:ERP_TAG)  { $env:ERP_TAG }  else { 'v1.26.09.22' }
$AbTag     = if ($env:AB_TAG)   { $env:AB_TAG }   else { 'v1.26.09.19' }
$ToolTag   = if ($env:TOOL_TAG) { $env:TOOL_TAG } else { 'v1.26.09.11' }

$ErpPort = if ($env:ERP_PORT) { [int]$env:ERP_PORT } else { 5080 }
$AbPort  = if ($env:AB_PORT)  { [int]$env:AB_PORT }  else { 5290 }
$ErpUrl  = "http://127.0.0.1:$ErpPort"

$DbName = if ($env:DB_NAME) { $env:DB_NAME } else { 'aierp' }
$DbUser = if ($env:DB_USER) { $env:DB_USER } else { 'aierp' }
$DbPort = if ($env:DB_PORT) { [int]$env:DB_PORT } else { 5432 }

# ERP auto-creates this default admin on first run; the agent logs in with it.
$ErpAdminEmail = 'erp@webvella.com'
$ErpAdminPass  = 'erp'

$InstallRoot = if ($env:INSTALL_ROOT) { $env:INSTALL_ROOT } else { Join-Path $env:LOCALAPPDATA 'aierp' }
$ErpDir = Join-Path $InstallRoot 'erp'
$AbDir  = Join-Path $InstallRoot 'agentbridge'
$LogDir = Join-Path $InstallRoot 'logs'

function Log($m)  { Write-Host "==> $m" -ForegroundColor Cyan }
function Warn($m) { Write-Host "[!] $m" -ForegroundColor Yellow }
function Read-Line($prompt) {
    Write-Host "$prompt " -NoNewline
    if ([Console]::IsInputRedirected) { return [Console]::In.ReadLine() }
    return Read-Host
}
function Ask($label, $default='') {
    $p = if ($default) { "$label [$default]:" } else { "${label}:" }
    $v = Read-Line $p
    if ([string]::IsNullOrWhiteSpace($v)) { return $default } else { return $v }
}
function New-RandomHex($bytes) {
    $b = New-Object byte[] $bytes
    [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($b)
    ($b | ForEach-Object { $_.ToString('x2') }) -join ''
}

# --- Provider presets: id -> @{Protocol;Base;Endpoint;Model} ---------------
function Get-ProviderSpec($id) {
    switch ($id) {
        'openai'     { @{Protocol='OpenAI';    Base='https://api.openai.com/'; Endpoint='v1/chat/completions'; Model='gpt-4o-mini'} }
        'anthropic'  { @{Protocol='Anthropic'; Base='https://api.anthropic.com/'; Endpoint='v1/messages'; Model='claude-3-5-sonnet-latest'} }
        'google'     { @{Protocol='Gemini';    Base='https://generativelanguage.googleapis.com/v1beta/models/__MODEL__:generateContent'; Endpoint=''; Model='gemini-1.5-flash'} }
        'mistral'    { @{Protocol='OpenAI';    Base='https://api.mistral.ai/'; Endpoint='v1/chat/completions'; Model='mistral-large-latest'} }
        'xai'        { @{Protocol='OpenAI';    Base='https://api.x.ai/'; Endpoint='v1/chat/completions'; Model='grok-2-latest'} }
        'deepseek'   { @{Protocol='OpenAI';    Base='https://api.deepseek.com/'; Endpoint='v1/chat/completions'; Model='deepseek-chat'} }
        'perplexity' { @{Protocol='OpenAI';    Base='https://api.perplexity.ai/'; Endpoint='chat/completions'; Model='llama-3.1-sonar-large-128k-online'} }
        'together'   { @{Protocol='OpenAI';    Base='https://api.together.xyz/'; Endpoint='v1/chat/completions'; Model='meta-llama/Llama-3.3-70B-Instruct-Turbo'} }
        'meta'       { @{Protocol='OpenAI';    Base='https://api.llama.com/compat/v1/'; Endpoint='chat/completions'; Model='llama-3.3-70b-versatile'} }
        'cohere'     { @{Protocol='OpenAI';    Base='https://api.cohere.com/compatibility/v1/'; Endpoint='chat/completions'; Model='command-r-plus'} }
        'zai'        { @{Protocol='OpenAI';    Base='https://api.z.ai/'; Endpoint='api/paas/v4/chat/completions'; Model='glm-4-plus'} }
        'ollama'     { @{Protocol='OpenAI';    Base='http://localhost:11434/'; Endpoint='v1/chat/completions'; Model='llama3.1'} }
        default      { $null }
    }
}

# --- Wizard ---------------------------------------------------------------
Write-Host ''
Log 'AI ERP setup - company details'
Write-Host 'Stored in the ERP and used on documents. Press Enter to keep a default.'
Write-Host ''
$Company   = Ask 'Company name (ragione sociale)'
if ([string]::IsNullOrWhiteSpace($Company)) { throw 'Company name is required.' }
$Legal     = Ask 'Legal name' $Company
$Vat       = Ask 'VAT number (Partita IVA)'
$TaxCode   = Ask 'Tax code (Codice Fiscale)' ''
$Street    = Ask 'Street address'
$City      = Ask 'City'
$Postal    = Ask 'Postal code'
$Country   = Ask 'Country' 'Italy'
$Email     = Ask 'Email'
$Pec       = Ask 'Certified email (PEC)' ''
$Phone     = Ask 'Phone'
$Iban      = Ask 'IBAN' ''
$Bank      = Ask 'Bank name' ''
$Currency  = Ask 'Currency' 'EUR'
$VatRegime = Ask 'VAT regime' 'ordinary'
$Tz        = (Get-TimeZone).Id
$Tz        = Ask 'Timezone' $Tz

Write-Host ''
Log 'AI provider (powers the assistant)'
Write-Host 'Pick one: openai, anthropic, google, mistral, cohere, meta, xai,'
Write-Host '          deepseek, perplexity, together, zai, ollama, custom'
$Provider = (Ask 'Provider' 'deepseek').ToLower()
if ($Provider -eq 'custom') {
    $Protocol = Ask 'Protocol (OpenAI / Gemini / Anthropic)' 'OpenAI'
    $Base     = Ask 'API base URL (ends with /)'
    $Endpoint = Ask 'Endpoint path' 'v1/chat/completions'
    $Model    = Ask 'Model name'
} else {
    $spec = Get-ProviderSpec $Provider
    if (-not $spec) { throw "Unknown provider '$Provider'." }
    $Protocol = $spec.Protocol; $Base = $spec.Base; $Endpoint = $spec.Endpoint
    $Model = Ask 'Model' $spec.Model
}
$KeyPlain = Read-Line "API key for ${Provider}:"
if ([string]::IsNullOrWhiteSpace($KeyPlain)) { throw 'An API key is required for the assistant.' }

# --- PostgreSQL -----------------------------------------------------------
$DbPassword = if ($env:DB_PASSWORD) { $env:DB_PASSWORD } else { New-RandomHex 16 }

function Find-Psql {
    $c = Get-Command psql -ErrorAction SilentlyContinue
    if ($c) { return $c.Source }
    $guess = Get-ChildItem 'C:\Program Files\PostgreSQL\*\bin\psql.exe' -ErrorAction SilentlyContinue | Sort-Object FullName -Descending | Select-Object -First 1
    if ($guess) { return $guess.FullName }
    return $null
}

if ($env:SKIP_PG_INSTALL -ne '1') {
    Log 'Checking PostgreSQL 16...'
    $psql = Find-Psql
    $hasV16 = $false
    if ($psql) { $v = (& $psql --version); if ($v -match '\s16\.') { $hasV16 = $true } }
    if ($hasV16) { Log 'PostgreSQL 16 already installed.' }
    else {
        # Direct EDB download is the primary method: it works reliably and avoids
        # winget's downloader being rejected by the EDB CDN (403), which would
        # otherwise rate-limit the fallback too.
        Log 'Downloading PostgreSQL 16 (EDB installer)...'
        $edbUrl = 'https://get.enterprisedb.com/postgresql/postgresql-16.15-4-windows-x64.exe'
        $edbExe = Join-Path $env:TEMP 'postgresql-16.15-4-windows-x64.exe'
        try {
            [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
            Invoke-WebRequest -Uri $edbUrl -OutFile $edbExe -UseBasicParsing
        } catch {
            if (Get-Command winget -ErrorAction SilentlyContinue) {
                Log 'Direct download failed; trying winget...'
                winget install -e --id PostgreSQL.PostgreSQL.16 --accept-source-agreements --accept-package-agreements --override "--mode unattended --unattendedmodeui none --superpassword $DbPassword"
            } else { throw "Could not download PostgreSQL: $($_.Exception.Message). Install it manually or set SKIP_PG_INSTALL=1." }
        }
        if (Test-Path $edbExe) {
            Log 'Running EDB installer (unattended)...'
            # Valid EDB InstallBuilder options only. Default port is 5433, so set 5432
            # to match the ERP. Keep server + commandlinetools (psql); skip pgAdmin/stackbuilder.
            $p = Start-Process -FilePath $edbExe -Verb RunAs -ArgumentList @(
                '--mode','unattended','--unattendedmodeui','none',
                '--superpassword', $DbPassword,
                '--servicename','postgresql-x64-16',
                '--serverport', "$DbPort",
                '--enable-components','server,commandlinetools',
                '--disable-components','pgAdmin,stackbuilder',
                '--create_shortcuts','0',
                '--debugtrace', (Join-Path $env:TEMP 'edb-install-trace.log')
            ) -Wait -PassThru
            if ($p.ExitCode -ne 0) { throw "EDB installer failed with exit code $($p.ExitCode). See %TEMP%\edb-install-trace.log" }
        }
        $psql = Find-Psql
    }
}
$psql = Find-Psql
if (-not $psql) { throw 'psql not found after install.' }

Log "Creating database '$DbName' and user '$DbUser'..."
$env:PGPASSWORD = $DbPassword
$psqlDir = Split-Path $psql -Parent
# Create role/db via the postgres superuser if needed; try as current admin first.
function Pg-Scalar($sql) { & $psql -U postgres -h localhost -p $DbPort -tAc $sql 2>$null }
$roleExists = Pg-Scalar "SELECT 1 FROM pg_roles WHERE rolname='$DbUser'"
if (-not $roleExists) {
    # The ERP creates casts between the built-in text/uuid types on first run,
    # which requires a superuser role.
    & $psql -U postgres -h localhost -p $DbPort -c "CREATE ROLE $DbUser LOGIN SUPERUSER PASSWORD '$DbPassword';" 2>$null | Out-Null
}
$dbExists = Pg-Scalar "SELECT 1 FROM pg_database WHERE datname='$DbName'"
if (-not $dbExists) {
    & $psql -U postgres -h localhost -p $DbPort -c "CREATE DATABASE $DbName OWNER $DbUser;" 2>$null | Out-Null
}
Log 'Database ready.'

# --- Download + extract ---------------------------------------------------
New-Item -ItemType Directory -Force -Path $InstallRoot,$ErpDir,$AbDir,$LogDir | Out-Null
function Get-Extract($repo, $tag, $asset, $dest) {
    $url = "https://github.com/$repo/releases/download/$tag/$asset"
    Log "Downloading $asset ..."
    $tmp = Join-Path $InstallRoot $asset
    Invoke-WebRequest -Uri $url -OutFile $tmp -UseBasicParsing
    if ($asset -like '*.tar.gz') { tar -xzf $tmp -C $dest }
    else { Expand-Archive -Path $tmp -DestinationPath $dest -Force }
    Remove-Item $tmp -Force
    Log "Extracted to $dest"
}
Get-Extract $ErpRepo  $ErpTag  'aierp-win-x64.tar.gz'          $ErpDir
Get-Extract $AbRepo   $AbTag   'agentbridge-win-x64.tar.gz'    $AbDir
Get-Extract $ToolRepo $ToolTag "ErpTool-$($ToolTag.TrimStart('v')).zip" (Join-Path $InstallRoot 'erptool')

# --- ERP config -----------------------------------------------------------
Log 'Writing ERP config.json ...'
$conn = "Server=localhost;Port=$DbPort;User Id=$DbUser;Password=$DbPassword;Database=$DbName;Pooling=true;MinPoolSize=1;MaxPoolSize=100;CommandTimeout=120;Timeout=120;KeepAlive=120;"
$erpConfig = [ordered]@{
    Settings = [ordered]@{
        ConnectionString = $conn
        EncryptionKey = (New-RandomHex 32).ToUpper()
        Lang = 'en'; Locale = 'en-US'; TimeZoneName = $Tz; CacheKey = ''
        DevelopmentMode = 'false'; EnableBackgroundJobs = 'true'; EnableFileSystemStorage = 'false'
        EmailEnabled = $false
        AppName = $Company; NavLogoUrl = ''; SystemMasterBackgroundImageUrl = ''
        Jwt = [ordered]@{ Key = (New-RandomHex 48); Issuer = 'ai-erp'; Audience = 'ai-erp' }
    }
}
$erpConfig | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $ErpDir 'config.json') -Encoding UTF8

# --- Company seed ---------------------------------------------------------
Log 'Customizing the company seed in bootstrap.json ...'
$bf = Join-Path $ErpDir 'bootstrap.json'
if (Test-Path $bf) {
    $boot = Get-Content $bf -Raw | ConvertFrom-Json
    $addr = "$Street`n$Postal, $City $Country"
    $companyRow = [ordered]@{
        name = $Company; legal_name = $Legal; vat_number = $Vat; address = $addr
        city = $City; country = $Country; email = $Email; phone = $Phone
        currency = $Currency; default_warehouse_id = '@warehouse:WH1'
    }
    $boot.seed.company = @($companyRow)
    $boot | ConvertTo-Json -Depth 30 | Set-Content $bf -Encoding UTF8
    Log "Company seed set to: $Company"
} else { Warn 'bootstrap.json not found; skipping company seed.' }

# --- AgentBridge config ---------------------------------------------------
Log "Configuring AgentBridge (provider=$Provider, protocol=$Protocol) ..."
$pd = Join-Path $AbDir 'PersistentData'
New-Item -ItemType Directory -Force -Path $pd | Out-Null
$base = $Base
if ($Protocol -eq 'Gemini') { $base = $Base.Replace('__MODEL__', $Model) }
$providerObj = @([ordered]@{
    ProviderName = $Provider; IsDefault = $true; Protocol = $Protocol; CacheType = 'PrefixCache'
    ModelName = $Model; ApiKey = $KeyPlain; BaseAddress = $base; EndPoint = $Endpoint
    Timeout = '00:01:00'; PauseBetweenRequests = '00:00:00'; ContextWindow = 128000
})
$providerObj | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $pd 'providers.json') -Encoding UTF8
@{ baseUrl = $ErpUrl; user = $ErpAdminEmail; password = $ErpAdminPass } |
    ConvertTo-Json -Depth 4 | Set-Content (Join-Path $pd 'erp.json') -Encoding UTF8

# Place the ErpTool plugin where the host scans: <AbDir>\Tools\ErpTool\
$toolSrc = Join-Path $InstallRoot 'erptool'
if (Test-Path $toolSrc) {
    $toolDst = Join-Path $AbDir 'Tools\ErpTool'
    New-Item -ItemType Directory -Force -Path $toolDst | Out-Null
    Copy-Item -Path (Join-Path $toolSrc '*') -Destination $toolDst -Recurse -Force
    Log "ErpTool plugin installed at $toolDst"
}

# --- Start services -------------------------------------------------------
Log 'Starting ERP and AgentBridge ...'
$erpExe = Join-Path $ErpDir 'AI.Erp.Site.exe'
if (Test-Path $erpExe) {
    Start-Process -FilePath $erpExe -ArgumentList "--urls=$ErpUrl" -WorkingDirectory $ErpDir `
        -RedirectStandardOutput (Join-Path $LogDir 'erp.log') -RedirectStandardError (Join-Path $LogDir 'erp.err.log')
} else { Warn "ERP exe not found at $erpExe" }

# Wait for the ERP to come up.
$up = $false
for ($i=0; $i -lt 60; $i++) {
    try { Invoke-WebRequest "$ErpUrl/manifest.webmanifest" -UseBasicParsing -TimeoutSec 3 | Out-Null; $up = $true; break }
    catch { Start-Sleep -Seconds 2 }
}
if ($up) { Log 'ERP is up.' } else { Warn 'ERP did not respond within 120s; check logs.' }

$abExe = Join-Path $AbDir 'AgentBridge.exe'
if (Test-Path $abExe) {
    $env:ERP_BASE_URL = $ErpUrl; $env:ERP_USER = $ErpAdminEmail; $env:ERP_PASSWORD = $ErpAdminPass
    Start-Process -FilePath $abExe -WorkingDirectory $AbDir `
        -RedirectStandardOutput (Join-Path $LogDir 'agentbridge.log') -RedirectStandardError (Join-Path $LogDir 'agentbridge.err.log')
    Log 'AgentBridge started.'
}

# --- PWA launcher (Desktop + Start Menu) ----------------------------------
Log 'Installing the ERP launcher (PWA app window) ...'
$icon = Join-Path $ErpDir 'wwwroot\assets\pwa-512x512.png'
$browser = $null
foreach ($b in @(
    "$env:ProgramFiles(x86)\Microsoft\Edge\Application\msedge.exe",
    "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe",
    "$env:ProgramFiles\Google\Chrome\Application\chrome.exe",
    "${env:ProgramFiles(x86)}\Google\Chrome\Application\chrome.exe")) {
    if (Test-Path $b) { $browser = $b; break }
}
if (-not $browser) { Warn 'No Edge/Chrome found; the PWA install prompt needs one.' }

function Make-Shortcut($path) {
    $ws = New-Object -ComObject WScript.Shell
    $sc = $ws.CreateShortcut($path)
    if ($browser) {
        $sc.TargetPath = $browser
        $sc.Arguments = "--app=$ErpUrl/?pwa=1"
    } else {
        $sc.TargetPath = $ErpUrl
    }
    if (Test-Path $icon) { $sc.IconLocation = "$icon,0" }
    $sc.WorkingDirectory = $ErpDir
    $sc.Description = "$Company - AI ERP"
    $sc.Save()
}
$desktop = [Environment]::GetFolderPath('Desktop')
$startMenu = Join-Path ([Environment]::GetFolderPath('Programs')) 'AI ERP'
New-Item -ItemType Directory -Force -Path $startMenu | Out-Null
Make-Shortcut (Join-Path $desktop 'AI ERP.lnk')
Make-Shortcut (Join-Path $startMenu 'AI ERP.lnk')
Log 'Launcher installed on Desktop and Start Menu.'

# Open the ERP.
if ($browser) { Start-Process $browser -ArgumentList "--app=$ErpUrl/?pwa=1" }
else { Start-Process $ErpUrl }

Write-Host ''
Log 'Done.'
Write-Host "  ERP URL:        $ErpUrl"
Write-Host "  DB user/pass:   $DbUser / $DbPassword   (in $ErpDir\config.json)"
Write-Host "  Install root:   $InstallRoot"
Write-Host ''
Write-Host '  ERP administrator (created automatically):'
Write-Host "    email:    $ErpAdminEmail"
Write-Host "    password: $ErpAdminPass"
Write-Host '    >>> Change this password immediately after the first login. <<<'
Write-Host ''
Write-Host "  To finish the PWA install, open the ERP in Edge/Chrome and click the"
Write-Host "  'Install' icon in the address bar (the app icon is already on your desktop)."
