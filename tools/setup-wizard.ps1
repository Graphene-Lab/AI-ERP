# AI ERP ecosystem setup wizard (Windows / PowerShell).
#
# Installs and connects the three parts of the agentic ERP:
#   AI ERP  +  AgentBridge  +  ErpTool plugin
# It asks a few questions about your company in the language of this machine
# (en, it, fr, es, de, ru), writes the company setup file (bootstrap.json),
# prepares the database, installs AgentBridge and the ErpTool plugin, and checks
# that the ERP is reachable.
#
# Non-interactive mode (for testing / automation): set $env:WIZARD_NONINTERACTIVE=1
# and provide the answers as the env vars listed in Collect-Answers.

$ErrorActionPreference = 'Stop'

# --- Localisation ---------------------------------------------------------
function Detect-Lang {
  if ($env:WIZARD_LANG) { $l = $env:WIZARD_LANG } else { $l = (Get-Culture).TwoLetterISOLanguageName }
  if (@('it','fr','es','de','ru') -contains $l) { return $l } else { return 'en' }
}
$Lang = Detect-Lang

$T = @{
  en = @{
    welcome='AI ERP ecosystem setup wizard'; q_company='Company name'; q_legal='Legal name'
    q_vat='VAT number'; q_address='Address'; q_city='City'; q_country='Country'
    q_currency='Default currency (e.g. EUR)'; q_dbhost='PostgreSQL host'; q_dbport='PostgreSQL port'
    q_dbuser='PostgreSQL user'; q_dbpass='PostgreSQL password'; q_dbname='Database name'
    q_user='ERP account email (the agent logs in as this user)'; q_pass='ERP account password'
    q_abdir='AgentBridge install folder'
    generating='Writing the company setup file (bootstrap.json)...'
    settingup='Preparing the database...'
    installing='Installing AgentBridge and the ErpTool plugin...'
    done='Setup complete. Start AgentBridge and talk to the agent.'
    verify_ok='ERP is reachable and the setup is applied.'
    verify_fail='Could not reach the ERP setup status. Check the ERP log.'
  }
  it = @{
    welcome="Procedura di installazione dell'ecosistema AI ERP"; q_company='Nome azienda'
    q_legal='Ragione sociale'; q_vat='Partita IVA'; q_address='Indirizzo'; q_city='Citta'
    q_country='Paese'; q_currency='Valuta predefinita (es. EUR)'; q_dbhost='Host PostgreSQL'
    q_dbport='Porta PostgreSQL'; q_dbuser='Utente PostgreSQL'; q_dbpass='Password PostgreSQL'
    q_dbname='Nome database'; q_user='Email account ERP (l''agente opera come questo utente)'
    q_pass='Password account ERP'; q_abdir='Cartella di installazione di AgentBridge'
    generating='Scrittura del file di setup azienda (bootstrap.json)...'
    settingup='Preparazione del database...'
    installing='Installazione di AgentBridge e del plugin ErpTool...'
    done="Installazione completata. Avvia AgentBridge e parla con l'agente."
    verify_ok='ERP raggiungibile e setup applicato.'
    verify_fail="Impossibile raggiungere lo stato setup dell'ERP. Controlla il log ERP."
  }
  fr = @{
    welcome="Programme d'installation de l'ecosysteme AI ERP"; q_company="Nom de l'entreprise"
    q_legal='Raison sociale'; q_vat='Numero TVA'; q_address='Adresse'; q_city='Ville'
    q_country='Pays'; q_currency='Devise par defaut (ex. EUR)'; q_dbhost='Hote PostgreSQL'
    q_dbport='Port PostgreSQL'; q_dbuser='Utilisateur PostgreSQL'; q_dbpass='Mot de passe PostgreSQL'
    q_dbname="Nom de la base de donnees"; q_user="E-mail du compte ERP (l'agent agit en tant que cet utilisateur)"
    q_pass='Mot de passe du compte ERP'; q_abdir="Dossier d'installation d'AgentBridge"
    generating="Ecriture du fichier de configuration de l'entreprise (bootstrap.json)..."
    settingup='Creation de la base de donnees...'
    installing="Installation d'AgentBridge et du plugin ErpTool..."
    done='Installation terminee. Demarrez AgentBridge et parlez a l''agent.'
    verify_ok='ERP accessible et configuration appliquee.'
    verify_fail="Impossible d'atteindre l'etat de configuration de l'ERP."
  }
  es = @{
    welcome='Asistente de instalacion del ecosistema de AI ERP'; q_company='Nombre de la empresa'
    q_legal='Razon social'; q_vat='Numero de IVA'; q_address='Direccion'; q_city='Ciudad'
    q_country='Pais'; q_currency='Moneda predeterminada (p. ej. EUR)'; q_dbhost='Host de PostgreSQL'
    q_dbport='Puerto de PostgreSQL'; q_dbuser='Usuario de PostgreSQL'; q_dbpass='Contrasena de PostgreSQL'
    q_dbname='Nombre de la base de datos'; q_user='Correo de la cuenta ERP (el agente actua como este usuario)'
    q_pass='Contrasena de la cuenta ERP'; q_abdir='Carpeta de instalacion de AgentBridge'
    generating='Escribiendo el archivo de configuracion de la empresa (bootstrap.json)...'
    settingup='Preparando la base de datos...'
    installing='Instalando AgentBridge y el plugin ErpTool...'
    done='Instalacion completada. Inicie AgentBridge y hable con el agente.'
    verify_ok='ERP accesible y configuracion aplicada.'
    verify_fail='No se pudo acceder al estado de configuracion del ERP.'
  }
  de = @{
    welcome='Installationsassistent fuer das AI-ERP-Oekosystem'; q_company='Firmenname'
    q_legal='Gesellschaftsname'; q_vat='USt-Nummer'; q_address='Adresse'; q_city='Stadt'
    q_country='Land'; q_currency='Standardwaehrung (z. B. EUR)'; q_dbhost='PostgreSQL-Host'
    q_dbport='PostgreSQL-Port'; q_dbuser='PostgreSQL-Benutzer'; q_dbpass='PostgreSQL-Passwort'
    q_dbname='Datenbankname'; q_user='ERP-Konto-E-Mail (der Agent handelt als dieser Benutzer)'
    q_pass='ERP-Konto-Passwort'; q_abdir='AgentBridge-Installationsordner'
    generating='Schreibe die Firmendatei (bootstrap.json)...'
    settingup='Datenbank wird vorbereitet...'
    installing='Installiere AgentBridge und das ErpTool-Plugin...'
    done='Setup abgeschlossen. Starten Sie AgentBridge und sprechen Sie mit dem Agenten.'
    verify_ok='ERP erreichbar und Setup angewendet.'
    verify_fail='ERP-Setup-Status nicht erreichbar. ERP-Log pruefen.'
  }
  ru = @{
    welcome='Мастер установки экосистемы AI ERP'; q_company='Название компании'
    q_legal='Юридическое название'; q_vat='Номер НДС'; q_address='Адрес'; q_city='Город'
    q_country='Страна'; q_currency='Валюта по умолчанию (напр. EUR)'; q_dbhost='Хост PostgreSQL'
    q_dbport='Порт PostgreSQL'; q_dbuser='Пользователь PostgreSQL'; q_dbpass='Пароль PostgreSQL'
    q_dbname='Имя базы данных'; q_user='Эл. почта учётной записи ERP (агент работает под этим пользователем)'
    q_pass='Пароль учётной записи ERP'; q_abdir='Папка установки AgentBridge'
    generating='Запись файла настройки компании (bootstrap.json)...'
    settingup='Подготовка базы данных...'
    installing='Установка AgentBridge и плагина ErpTool...'
    done='Установка завершена. Запустите AgentBridge и общайтесь с агентом.'
    verify_ok='ERP доступен, настройка применена.'
    verify_fail='Не удалось получить статус настройки ERP. Проверьте журнал ERP.'
  }
}
function Msg($key) { if ($T[$Lang].ContainsKey($key)) { $T[$Lang][$key] } else { $T['en'][$key] } }

function Ask($key, $default) {
  if ($env:WIZARD_NONINTERACTIVE -eq '1') {
    $envName = $key.ToUpper()
    $v = [Environment]::GetEnvironmentVariable("WIZARD_$envName")
    if ($null -ne $v) { return $v }
    return $default
  }
  $ans = Read-Host "$(Msg $key) [$default]"
  if ([string]::IsNullOrWhiteSpace($ans)) { return $default } else { return $ans }
}

# --- Paths --------------------------------------------------------------
$RepoDir = if ($env:AI_ERP_DIR) { $env:AI_ERP_DIR } else { Split-Path -Parent $PSScriptRoot }
$Bootstrap = Join-Path $RepoDir 'AI.Erp.Site\bootstrap.json'

# --- Collect answers ----------------------------------------------------
Write-Host "== $(Msg 'welcome') ($($Lang.ToUpper())) =="
$a = @{}
$a['company']  = Ask 'q_company'  'Northwind Traders'
$a['legal']    = Ask 'q_legal'    'Northwind Traders S.r.l.'
$a['vat']      = Ask 'q_vat'      'IT01234560123'
$a['address']  = Ask 'q_address'  'Via Roma 10'
$a['city']     = Ask 'q_city'     'Milan'
$a['country']  = Ask 'q_country'  'Italy'
$a['currency'] = Ask 'q_currency' 'EUR'
$a['dbhost']   = Ask 'q_dbhost'   '127.0.0.1'
$a['dbport']   = Ask 'q_dbport'   '5432'
$a['dbuser']   = Ask 'q_dbuser'   'dev'
$a['dbpass']   = Ask 'q_dbpass'   'dev'
$a['dbname']   = Ask 'q_dbname'   'erp'
$a['user']     = Ask 'q_user'     'erp@webvella.com'
$a['pass']     = Ask 'q_pass'     'erp'
$a['abdir']    = Ask 'q_abdir'    (Join-Path $env:LOCALAPPDATA 'AgentBridge')

# --- Write the company record into bootstrap.json -----------------------
Write-Host (Msg 'generating')
if (-not (Test-Path $Bootstrap)) { throw "bootstrap.json not found at $Bootstrap" }
Copy-Item $Bootstrap "$Bootstrap.bak" -Force
$json = Get-Content $Bootstrap -Raw -Encoding UTF8 | ConvertFrom-Json
$rec = [ordered]@{
  name = $a['company']; legal_name = $a['legal']; vat_number = $a['vat']
  address = $a['address']; city = $a['city']; country = $a['country']
  email = $a['user']; phone = ''; currency = $a['currency']
  default_warehouse_id = '@warehouse:WH1'
}
if (-not $json.seed) { $json | Add-Member -NotePropertyName seed -NotePropertyValue ([pscustomobject]@{}) }
$json.seed.company = @($rec)
$json | ConvertTo-Json -Depth 50 | Set-Content $Bootstrap -Encoding UTF8
Write-Host "  updated company record in $Bootstrap"

# --- Prepare the database ---------------------------------------------
Write-Host (Msg 'settingup')
Write-Host "  Set AI.Erp.Site/config.json -> Settings.ConnectionString to:"
Write-Host "  Server=$($a['dbhost']);Port=$($a['dbport']);User Id=$($a['dbuser']);Password=$($a['dbpass']);Database=$($a['dbname']);Pooling=true;"

# --- Install AgentBridge + ErpTool plugin -----------------------------
Write-Host (Msg 'installing')
New-Item -ItemType Directory -Force -Path $a['abdir'] | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $a['abdir'] 'PersistentData') | Out-Null
$erpJson = [ordered]@{
  baseUrl = $(if ($env:ERP_BASE_URL) { $env:ERP_BASE_URL } else { 'http://127.0.0.1:5080' })
  user = $a['user']; password = $a['pass']
}
$erpPath = Join-Path $a['abdir'] 'PersistentData\erp.json'
$erpJson | ConvertTo-Json | Set-Content $erpPath -Encoding UTF8
Write-Host "  -> $erpPath"
Write-Host "  AgentBridge: https://github.com/Graphene-Lab/AgentBridge/releases/latest"
Write-Host "  ErpTool plugin -> Tools/ErpTool/ : https://github.com/Graphene-Lab/ErpTool/releases/latest"

# --- Verify -----------------------------------------------------------
try {
  $base = if ($env:ERP_BASE_URL) { $env:ERP_BASE_URL } else { 'http://127.0.0.1:5080' }
  $login = Invoke-RestMethod -Method Post -Uri "$base/api/v3/en_US/auth/jwt/token" `
    -ContentType 'application/json' -Body (@{ email = $a['user']; password = $a['pass'] } | ConvertTo-Json)
  $token = $login.object
  $status = Invoke-RestMethod -Method Get -Uri "$base/api/v3.0/p/agent/setup-status" `
    -Headers @{ Authorization = "Bearer $token" }
  if ($status.data.installed) { Write-Host (Msg 'verify_ok') } else { Write-Host (Msg 'verify_fail') }
} catch {
  Write-Host (Msg 'verify_fail')
}

Write-Host ''
Write-Host (Msg 'done')
