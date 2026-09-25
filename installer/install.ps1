# AI ERP - one-shot installer for Windows.
# Invoked by install.bat. Prepares a complete machine: PostgreSQL 16, AI ERP,
# AgentBridge + ErpTool, the company seed, and a PWA launcher.
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

# --- Constants -------------------------------------------------------------
$ErpRepo   = 'Graphene-Lab/AI-ERP'
$AbRepo    = 'Graphene-Lab/AgentBridge'
$ToolRepo  = 'Graphene-Lab/ErpTool'
$ErpTag    = if ($env:ERP_TAG)  { $env:ERP_TAG }  else { 'v1.26.09.25' }
$AbTag     = if ($env:AB_TAG)   { $env:AB_TAG }   else { 'v1.26.09.22' }
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
# The ERP logs into PostgreSQL with the password we write into config.json, so a
# re-run of this installer must never leave the two out of sync. Older versions
# generated a brand-new random password on every run, which broke the existing
# database connection; this logic recovers from that state too:
#   1. Try every known password (env var, db_password.txt, the current
#      config.json) as the ERP database user. The first one that works wins.
#   2. If none works, try them as the postgres superuser and sync the ERP user
#      to the one that works.
#   3. If the postgres superuser password is unknown too (an old installer
#      generated a new random password on every run), briefly switch
#      pg_hba.conf to "trust", reset the ERP user password, restore
#      pg_hba.conf. Admin rights are requested for this step only.
$DbPasswordFile = Join-Path $InstallRoot 'db_password.txt'
$FreshPassword = New-RandomHex 16

function Get-PasswordFromErpConfig {
    $cfg = Join-Path $ErpDir 'config.json'
    if (-not (Test-Path $cfg)) { return $null }
    try {
        $cs = (Get-Content $cfg -Raw | ConvertFrom-Json).Settings.ConnectionString
        if ($cs -and $cs -match '(?i)Password=([^;]+)') { return $Matches[1] }
    } catch { }
    return $null
}

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
        $downloaded = $false
        try {
            [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
            Invoke-WebRequest -Uri $edbUrl -OutFile $edbExe -UseBasicParsing
            $downloaded = $true
        } catch {
            if (Get-Command winget -ErrorAction SilentlyContinue) {
                Log 'Direct download failed; trying winget...'
                # Pass --serverport too: the EDB installer defaults to 5433, but the
                # ERP and the psql calls below use $DbPort.
                winget install -e --id PostgreSQL.PostgreSQL.16 --accept-source-agreements --accept-package-agreements --override "--mode unattended --unattendedmodeui none --superpassword $FreshPassword --serverport $DbPort"
            } else { throw "Could not download PostgreSQL: $($_.Exception.Message). Install it manually or set SKIP_PG_INSTALL=1." }
        }
        # Only run the EDB installer when the download actually succeeded; a failed
        # Invoke-WebRequest can leave a partial $edbExe that must not be executed.
        if ($downloaded) {
            Log 'Running EDB installer (unattended)...'
            # Valid EDB InstallBuilder options only. Default port is 5433, so set $DbPort
            # to match the ERP. Keep server + commandlinetools (psql); skip pgAdmin/stackbuilder.
            $p = Start-Process -FilePath $edbExe -Verb RunAs -ArgumentList @(
                '--mode','unattended','--unattendedmodeui','none',
                '--superpassword', $FreshPassword,
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
$psqlDir = Split-Path $psql -Parent

function Test-PgAuth($user, $pass, $db) {
    if ([string]::IsNullOrWhiteSpace($pass)) { return $false }
    $env:PGPASSWORD = $pass
    & $psql -U $user -h localhost -p $DbPort -d $db -tAc 'SELECT 1' 2>$null | Out-Null
    return ($LASTEXITCODE -eq 0)
}

# Create/sync the ERP role and database using an authenticated postgres superuser.
# Single quotes in the password are doubled so they cannot break the SQL.
function Sync-PgFromSuperuser($pass) {
    $env:PGPASSWORD = $pass
    $esc = $pass.Replace("'", "''")
    $roleExists = (& $psql -U postgres -h localhost -p $DbPort -d postgres -tAc "SELECT 1 FROM pg_roles WHERE rolname='$DbUser'" 2>$null)
    if ($roleExists) {
        # Keep the ERP user in step with the password we are about to write into config.json.
        & $psql -U postgres -h localhost -p $DbPort -d postgres -c "ALTER ROLE $DbUser LOGIN SUPERUSER PASSWORD '$esc';" 2>$null | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "Could not update the password of the '$DbUser' database user." }
    } else {
        # The ERP creates casts between the built-in text/uuid types on first run,
        # which requires a superuser role.
        & $psql -U postgres -h localhost -p $DbPort -d postgres -c "CREATE ROLE $DbUser LOGIN SUPERUSER PASSWORD '$esc';" 2>$null | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "Could not create the '$DbUser' database user." }
    }
    $dbExists = (& $psql -U postgres -h localhost -p $DbPort -d postgres -tAc "SELECT 1 FROM pg_database WHERE datname='$DbName'" 2>$null)
    if (-not $dbExists) {
        & $psql -U postgres -h localhost -p $DbPort -d postgres -c "CREATE DATABASE $DbName OWNER $DbUser;" 2>$null | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "Could not create the '$DbName' database." }
    }
}

# Last resort: the postgres superuser password is unknown (an older installer
# generated a new one on every run). Ask for admin rights once, switch
# pg_hba.conf to "trust" for localhost only, reset the ERP user password, then
# always restore pg_hba.conf and restart the service.
function Repair-PgTrustAccess($pass) {
    Log 'Repairing database access: Windows administrator rights are needed for a moment.'
    $pgConfig = Join-Path $psqlDir 'pg_config.exe'
    if (-not (Test-Path $pgConfig)) {
        $gc = Get-Command pg_config -ErrorAction SilentlyContinue
        if ($gc) { $pgConfig = $gc.Source }
    }
    if (-not $pgConfig) { throw 'pg_config not found: cannot locate pg_hba.conf to repair database access.' }
    $sysconf = (& $pgConfig --sysconfdir).Trim()
    $hba = Join-Path $sysconf 'pg_hba.conf'
    if (-not (Test-Path $hba)) { throw "pg_hba.conf not found at $hba." }

    $paramFile = Join-Path $env:TEMP 'aierp-db-repair.json'
    $repairPs1 = Join-Path $env:TEMP 'aierp-db-repair.ps1'
    $repairLog = Join-Path $env:TEMP 'aierp-db-repair.log'
    @{ hba=$hba; psql=$psql; port=$DbPort; dbName=$DbName; dbUser=$DbUser; password=$pass } |
        ConvertTo-Json | Set-Content -Path $paramFile -Encoding UTF8

    $repairBody = @'
$ErrorActionPreference = 'Stop'
$log = Join-Path $env:TEMP 'aierp-db-repair.log'
try {
    $p = Get-Content -Raw -Encoding UTF8 (Join-Path $env:TEMP 'aierp-db-repair.json') | ConvertFrom-Json
    $svc = Get-Service -Name 'postgresql*' -ErrorAction SilentlyContinue | Sort-Object Name -Descending | Select-Object -First 1
    if (-not $svc) { throw 'PostgreSQL service not found.' }
    $backup = $p.hba + '.aierp-bak'
    Copy-Item $p.hba $backup -Force
    try {
        $lines = @(Get-Content $p.hba)
        (@('host all all 127.0.0.1/32 trust', 'host all all ::1/128 trust') + $lines) | Set-Content $p.hba -Encoding ASCII
        if ($svc.Status -eq 'Running') { Restart-Service $svc.Name -Force } else { Start-Service $svc.Name }
        for ($i = 0; $i -lt 30; $i++) { if ((Get-Service $svc.Name).Status -eq 'Running') { break }; Start-Sleep -Seconds 1 }
        $env:PGPASSWORD = ''
        & $p.psql -U postgres -h localhost -p $p.port -d postgres -tAc 'SELECT 1' | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'Trust connection failed.' }
        $role = & $p.psql -U postgres -h localhost -p $p.port -d postgres -tAc "SELECT 1 FROM pg_roles WHERE rolname='$($p.dbUser)'"
        $esc = $p.password.Replace("'", "''")
        if ($role) {
            & $p.psql -U postgres -h localhost -p $p.port -d postgres -c "ALTER ROLE $($p.dbUser) LOGIN SUPERUSER PASSWORD '$esc';" | Out-Null
        } else {
            & $p.psql -U postgres -h localhost -p $p.port -d postgres -c "CREATE ROLE $($p.dbUser) LOGIN SUPERUSER PASSWORD '$esc';" | Out-Null
        }
        if ($LASTEXITCODE -ne 0) { throw 'Could not reset the ERP database user password.' }
        $db = & $p.psql -U postgres -h localhost -p $p.port -d postgres -tAc "SELECT 1 FROM pg_database WHERE datname='$($p.dbName)'"
        if (-not $db) {
            & $p.psql -U postgres -h localhost -p $p.port -d postgres -c "CREATE DATABASE $($p.dbName) OWNER $($p.dbUser);" | Out-Null
            if ($LASTEXITCODE -ne 0) { throw 'Could not create the ERP database.' }
        }
    } finally {
        Copy-Item $backup $p.hba -Force
        Remove-Item $backup -Force -ErrorAction SilentlyContinue
        try { Restart-Service $svc.Name -Force } catch { }
    }
    'Database access repaired.' | Out-File $log -Encoding UTF8
    exit 0
} catch {
    "Repair failed: $($_.Exception.Message)" | Out-File $log -Encoding UTF8
    exit 1
}
'@
    Set-Content -Path $repairPs1 -Value $repairBody -Encoding UTF8
    $proc = Start-Process -FilePath (Join-Path $PSHOME 'powershell.exe') -Verb RunAs -Wait -PassThru `
        -ArgumentList '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$repairPs1`""
    if ($proc.ExitCode -ne 0) {
        $why = ''
        if (Test-Path $repairLog) { $why = (Get-Content $repairLog -Raw).Trim() }
        throw "Database access repair failed. $why"
    }
}

Log 'Checking database access ...'
$Candidates = @()
if ($env:DB_PASSWORD) { $Candidates += $env:DB_PASSWORD }
if (Test-Path $DbPasswordFile) { $Candidates += (Get-Content $DbPasswordFile -Raw).Trim() }
$cfgPass = Get-PasswordFromErpConfig
if ($cfgPass) { $Candidates += $cfgPass }
$Candidates += $FreshPassword
$Candidates = @($Candidates | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique)

$DbPassword = $null
foreach ($c in $Candidates) {
    if (Test-PgAuth $DbUser $c $DbName) {
        $DbPassword = $c
        Log "Reusing the existing '$DbUser' database password."
        break
    }
}
if (-not $DbPassword) {
    foreach ($c in $Candidates) {
        if (Test-PgAuth 'postgres' $c 'postgres') {
            $DbPassword = $c
            Log "Syncing the '$DbUser' database user with the known postgres password ..."
            Sync-PgFromSuperuser $c
            break
        }
    }
}
if (-not $DbPassword) {
    Log 'The existing PostgreSQL superuser password is unknown (an older installer generated a new one on every run).'
    Repair-PgTrustAccess $FreshPassword
    $DbPassword = $FreshPassword
}
if (-not (Test-PgAuth $DbUser $DbPassword $DbName)) {
    throw "Could not connect to the '$DbName' database as '$DbUser' after the repair. Check the PostgreSQL service and try again."
}
# Persist the password so a later re-run reuses it instead of generating a new one.
New-Item -ItemType Directory -Force -Path $InstallRoot | Out-Null
Set-Content -Path $DbPasswordFile -Value $DbPassword -Encoding ASCII -NoNewline
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
# Reuse the encryption and JWT keys from an existing config.json: regenerating
# them on a re-run would make data encrypted by the previous install unreadable.
$encKey = $null; $jwtKey = $null
$existingCfg = Join-Path $ErpDir 'config.json'
if (Test-Path $existingCfg) {
    try {
        $old = Get-Content $existingCfg -Raw | ConvertFrom-Json
        $encKey = $old.Settings.EncryptionKey
        $jwtKey = $old.Settings.Jwt.Key
    } catch { }
}
if ([string]::IsNullOrWhiteSpace($encKey)) { $encKey = (New-RandomHex 32).ToUpper() }
if ([string]::IsNullOrWhiteSpace($jwtKey)) { $jwtKey = New-RandomHex 48 }
$erpConfig = [ordered]@{
    Settings = [ordered]@{
        ConnectionString = $conn
        EncryptionKey = $encKey
        Lang = 'en'; Locale = 'en-US'; TimeZoneName = $Tz; CacheKey = ''
        DevelopmentMode = 'false'; EnableBackgroundJobs = 'true'; EnableFileSystemStorage = 'false'
        EmailEnabled = $false
        AppName = $Company; NavLogoUrl = ''; SystemMasterBackgroundImageUrl = ''
        Jwt = [ordered]@{ Key = $jwtKey; Issuer = 'ai-erp'; Audience = 'ai-erp' }
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
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText((Join-Path $pd 'providers.json'), ($providerObj | ConvertTo-Json -Depth 6), $utf8NoBom)
[System.IO.File]::WriteAllText((Join-Path $pd 'erp.json'),
    (@{ baseUrl = $ErpUrl; user = $ErpAdminEmail; password = $ErpAdminPass } | ConvertTo-Json -Depth 4), $utf8NoBom)

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

$abExe = Join-Path $AbDir 'agent.exe'
if (Test-Path $abExe) {
    $env:ERP_BASE_URL = $ErpUrl; $env:ERP_USER = $ErpAdminEmail; $env:ERP_PASSWORD = $ErpAdminPass
    Start-Process -FilePath $abExe -WorkingDirectory $AbDir `
        -RedirectStandardOutput (Join-Path $LogDir 'agentbridge.log') -RedirectStandardError (Join-Path $LogDir 'agentbridge.err.log')
    Log 'AgentBridge started.'
}

# --- Launcher script ------------------------------------------------------
# The desktop / Start Menu icon must not just open a browser to a URL: if the ERP
# is not running (after a reboot, or if it was closed) that URL gives
# ERR_CONNECTION_REFUSED. The launcher brings the ERP (and AgentBridge) up first,
# then opens the app window, so the icon works every time it is clicked.
Log 'Writing the launcher script ...'
$launcherPs1 = Join-Path $InstallRoot 'aierp-launch.ps1'
$launcherTemplate = @'
# AI ERP launcher. Ensures the ERP (and AgentBridge) are running, then opens the app.
$ErrorActionPreference = 'SilentlyContinue'
$ProgressPreference = 'SilentlyContinue'

$InstallRoot = '{{INSTALL_ROOT}}'
$ErpDir  = Join-Path $InstallRoot 'erp'
$AbDir   = Join-Path $InstallRoot 'agentbridge'
$LogDir  = Join-Path $InstallRoot 'logs'
$ErpPort = {{ERP_PORT}}
$AbPort  = {{AB_PORT}}
$ErpUrl  = "http://127.0.0.1:$ErpPort"
$AbUrl   = "http://127.0.0.1:$AbPort"
$ErpAdminEmail = 'erp@webvella.com'
$ErpAdminPass  = 'erp'

function Test-Http($url) {
    try { Invoke-WebRequest $url -UseBasicParsing -TimeoutSec 2 | Out-Null; return $true } catch { return $false }
}

New-Item -ItemType Directory -Force -Path $LogDir | Out-Null
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'

# Bring the ERP up if it is not already listening.
if (-not (Test-Http "$ErpUrl/manifest.webmanifest")) {
    $erpExe = Join-Path $ErpDir 'AI.Erp.Site.exe'
    if (Test-Path $erpExe) {
        Start-Process -FilePath $erpExe -ArgumentList "--urls=$ErpUrl" -WorkingDirectory $ErpDir -WindowStyle Hidden `
            -RedirectStandardOutput (Join-Path $LogDir "erp-$stamp.log") -RedirectStandardError (Join-Path $LogDir "erp-$stamp.err.log")
    }
    for ($i=0; $i -lt 90; $i++) { if (Test-Http "$ErpUrl/manifest.webmanifest") { break }; Start-Sleep -Seconds 2 }
}

# Bring AgentBridge up too (the assistant needs it). Best effort.
if (-not (Test-Http "$AbUrl/health")) {
    $abExe = Join-Path $AbDir 'agent.exe'
    if (Test-Path $abExe) {
        $env:ERP_BASE_URL = $ErpUrl; $env:ERP_USER = $ErpAdminEmail; $env:ERP_PASSWORD = $ErpAdminPass
        Start-Process -FilePath $abExe -WorkingDirectory $AbDir -WindowStyle Hidden `
            -RedirectStandardOutput (Join-Path $LogDir "agentbridge-$stamp.log") -RedirectStandardError (Join-Path $LogDir "agentbridge-$stamp.err.log")
    }
}

$browser = $null
foreach ($b in @(
    "$env:ProgramFiles(x86)\Microsoft\Edge\Application\msedge.exe",
    "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe",
    "$env:ProgramFiles\Google\Chrome\Application\chrome.exe",
    "${env:ProgramFiles(x86)}\Google\Chrome\Application\chrome.exe")) {
    if (Test-Path $b) { $browser = $b; break }
}

if (Test-Http "$ErpUrl/manifest.webmanifest") {
    if ($browser) { Start-Process $browser -ArgumentList "--app=$ErpUrl/?pwa=1" }
    else { Start-Process $ErpUrl }
} else {
    try {
        Add-Type -AssemblyName System.Windows.Forms
        [System.Windows.Forms.MessageBox]::Show(
            "AI ERP non riesce ad avviarsi.`r`nControlla i log in: $LogDir",
            'AI ERP', 'OK', 'Error') | Out-Null
    } catch {
        Write-Host "AI ERP non riesce ad avviarsi. Controlla i log in: $LogDir"
    }
}
'@
$launcherContent = $launcherTemplate.Replace('{{INSTALL_ROOT}}', $InstallRoot).Replace('{{ERP_PORT}}', "$ErpPort").Replace('{{AB_PORT}}', "$AbPort")
[System.IO.File]::WriteAllText($launcherPs1, $launcherContent, $utf8NoBom)

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
    # Point at the launcher, not the browser URL, so the icon also starts the services.
    $sc.TargetPath = Join-Path $PSHOME 'powershell.exe'
    $sc.Arguments = "-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File `"$launcherPs1`""
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
Write-Host ''
Write-Host "  The 'AI ERP' icon on the Desktop also starts the services. If the ERP is"
Write-Host "  not running (after a reboot, for example), just double-click the icon and it"
Write-Host "  will bring it back up before opening the window."
