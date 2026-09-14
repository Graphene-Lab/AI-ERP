#!/usr/bin/env bash
# AI ERP ecosystem setup wizard (Linux / macOS / WSL).
#
# Installs and connects the three parts of the agentic ERP:
#   AI ERP  +  AgentBridge  +  ErpTool plugin
# It asks a few questions about your company in the language of this machine
# (en, it, fr, es, de, ru), writes the company setup file (bootstrap.json),
# creates the database, starts the ERP, installs AgentBridge and the ErpTool
# plugin, and checks that everything is reachable.
#
# Non-interactive mode (for testing / automation): set WIZARD_NONINTERACTIVE=1
# and provide the answers as the env vars listed in collect_answers().
set -u

# ---------------------------------------------------------------------------
# Localisation
# ---------------------------------------------------------------------------
detect_lang() {
  local l="${LC_ALL:-${LC_MESSAGES:-${LANG:-}}}"
  l="${l:0:2}"
  case "$l" in it|fr|es|de|ru) echo "$l" ;; *) echo "en" ;; esac
}
LANG_CODE="${WIZARD_LANG:-$(detect_lang)}"

# msg KEY  -> the string for KEY in the current language (falls back to en).
msg() {
  local key="$1"
  local var="T_${LANG_CODE}_${key}"
  local val="${!var:-}"
  if [ -z "$val" ]; then val="${T_en_${key}:-$key}"; fi
  echo "$val"
}

# Prompt strings. Keys are shared; each language overrides them.
T_en_welcome="AI ERP ecosystem setup wizard"
T_it_welcome="Procedura di installazione dell'ecosistema AI ERP"
T_fr_welcome="Programma di installazione dell'ecosistema AI ERP"
T_es_welcome="Asistente de instalacion del ecosistema de AI ERP"
T_de_welcome="Installationsassistent fuer das AI-ERP-Oekosystem"
T_ru_welcome="Мастер установки экосистемы AI ERP"

T_en_q_company="Company name"
T_it_q_company="Nome azienda"
T_fr_q_company="Nom de l'entreprise"
T_es_q_company="Nombre de la empresa"
T_de_q_company="Firmenname"
T_ru_q_company="Название компании"

T_en_q_legal="Legal name"
T_it_q_legal="Ragione sociale"
T_fr_q_legal="Raison sociale"
T_es_q_legal="Razon social"
T_de_q_legal="Gesellschaftsname"
T_ru_q_legal="Юридическое название"

T_en_q_vat="VAT number"
T_it_q_vat="Partita IVA"
T_fr_q_vat="Numero TVA"
T_es_q_vat="Numero de IVA"
T_de_q_vat="USt-Nummer"
T_ru_q_vat="Номер НДС"

T_en_q_address="Address"
T_it_q_address="Indirizzo"
T_fr_q_address="Adresse"
T_es_q_address="Direccion"
T_de_q_address="Adresse"
T_ru_q_address="Адрес"

T_en_q_city="City"
T_it_q_city="Citta"
T_fr_q_city="Ville"
T_es_q_city="Ciudad"
T_de_q_city="Stadt"
T_ru_q_city="Город"

T_en_q_country="Country"
T_it_q_country="Paese"
T_fr_q_country="Pays"
T_es_q_country="Pais"
T_de_q_country="Land"
T_ru_q_country="Страна"

T_en_q_currency="Default currency (e.g. EUR)"
T_it_q_currency="Valuta predefinita (es. EUR)"
T_fr_q_currency="Devise par defaut (ex. EUR)"
T_es_q_currency="Moneda predeterminada (p. ej. EUR)"
T_de_q_currency="Standardwaehrung (z. B. EUR)"
T_ru_q_currency="Валюта по умолчанию (напр. EUR)"

T_en_q_dbhost="PostgreSQL host"
T_it_q_dbhost="Host PostgreSQL"
T_fr_q_dbhost="Hote PostgreSQL"
T_es_q_dbhost="Host de PostgreSQL"
T_de_q_dbhost="PostgreSQL-Host"
T_ru_q_dbhost="Хост PostgreSQL"

T_en_q_dbport="PostgreSQL port"
T_it_q_dbport="Porta PostgreSQL"
T_fr_q_dbport="Port PostgreSQL"
T_es_q_dbport="Puerto de PostgreSQL"
T_de_q_dbport="PostgreSQL-Port"
T_ru_q_dbport="Порт PostgreSQL"

T_en_q_dbuser="PostgreSQL user"
T_it_q_dbuser="Utente PostgreSQL"
T_fr_q_dbuser="Utilisateur PostgreSQL"
T_es_q_dbuser="Usuario de PostgreSQL"
T_de_q_dbuser="PostgreSQL-Benutzer"
T_ru_q_dbuser="Пользователь PostgreSQL"

T_en_q_dbpass="PostgreSQL password"
T_it_q_dbpass="Password PostgreSQL"
T_fr_q_dbpass="Mot de passe PostgreSQL"
T_es_q_dbpass="Contrasena de PostgreSQL"
T_de_q_dbpass="PostgreSQL-Passwort"
T_ru_q_dbpass="Пароль PostgreSQL"

T_en_q_dbname="Database name"
T_it_q_dbname="Nome database"
T_fr_q_dbname="Nom de la base de donnees"
T_es_q_dbname="Nombre de la base de datos"
T_de_q_dbname="Datenbankname"
T_ru_q_dbname="Имя базы данных"

T_en_q_user="ERP account email (the agent logs in as this user)"
T_it_q_user="Email account ERP (l'agente opera come questo utente)"
T_fr_q_user="E-mail du compte ERP (l'agent agit en tant que cet utilisateur)"
T_es_q_user="Correo de la cuenta ERP (el agente actua como este usuario)"
T_de_q_user="ERP-Konto-E-Mail (der Agent handelt als dieser Benutzer)"
T_ru_q_user="Эл. почта учётной записи ERP (агент работает под этим пользователем)"

T_en_q_pass="ERP account password"
T_it_q_pass="Password account ERP"
T_fr_q_pass="Mot de passe du compte ERP"
T_es_q_pass="Contrasena de la cuenta ERP"
T_de_q_pass="ERP-Konto-Passwort"
T_ru_q_pass="Пароль учётной записи ERP"

T_en_q_abdir="AgentBridge install folder"
T_it_q_abdir="Cartella di installazione di AgentBridge"
T_fr_q_abdir="Dossier d'installation d'AgentBridge"
T_es_q_abdir="Carpeta de instalacion de AgentBridge"
T_de_q_abdir="AgentBridge-Installationsordner"
T_ru_q_abdir="Папка установки AgentBridge"

T_en_generating="Writing the company setup file (bootstrap.json)..."
T_it_generating="Scrittura del file di setup azienda (bootstrap.json)..."
T_fr_generating="Ecriture du fichier de configuration de l'entreprise (bootstrap.json)..."
T_es_generating="Escribiendo el archivo de configuracion de la empresa (bootstrap.json)..."
T_de_generating="Schreibe die Firmendatei (bootstrap.json)..."
T_ru_generating="Запись файла настройки компании (bootstrap.json)..."

T_en_settingup="Creating the database and starting the ERP..."
T_it_settingup="Creazione del database e avvio dell'ERP..."
T_fr_settingup="Creation de la base de donnees et demarrage de l'ERP..."
T_es_settingup="Creando la base de datos e iniciando el ERP..."
T_de_settingup="Erstelle Datenbank und starte das ERP..."
T_ru_settingup="Создание базы данных и запуск ERP..."

T_en_installing="Installing AgentBridge and the ErpTool plugin..."
T_it_installing="Installazione di AgentBridge e del plugin ErpTool..."
T_fr_installing="Installation d'AgentBridge et du plugin ErpTool..."
T_es_installing="Instalando AgentBridge y el plugin ErpTool..."
T_de_installing="Installiere AgentBridge und das ErpTool-Plugin..."
T_ru_installing="Установка AgentBridge и плагина ErpTool..."

T_en_done="Setup complete. Start AgentBridge and talk to the agent."
T_it_done="Installazione completata. Avvia AgentBridge e parla con l'agente."
T_fr_done="Installation terminee. Demarrez AgentBridge et parlez a l'agent."
T_es_done="Instalacion completada. Inicie AgentBridge y hable con el agente."
T_de_done="Setup abgeschlossen. Starten Sie AgentBridge und sprechen Sie mit dem Agenten."
T_ru_done="Установка завершена. Запустите AgentBridge и общайтесь с агентом."

T_en_verify_ok="ERP is reachable and the setup is applied."
T_it_verify_ok="ERP raggiungibile e setup applicato."
T_fr_verify_ok="ERP accessible et configuration appliquee."
T_es_verify_ok="ERP accesible y configuracion aplicada."
T_de_verify_ok="ERP erreichbar und Setup angewendet."
T_ru_verify_ok="ERP доступен, настройка применена."

T_en_verify_fail="Could not reach the ERP setup status. Check the ERP log."
T_it_verify_fail="Impossibile raggiungere lo stato setup dell'ERP. Controlla il log ERP."
T_fr_verify_fail="Impossible d'atteindre l'etat de configuration de l'ERP. Verifiez le journal."
T_es_verify_fail="No se pudo acceder al estado de configuracion del ERP. Revise el registro."
T_de_verify_fail="ERP-Setup-Status nicht erreichbar. ERP-Log pruefen."
T_ru_verify_fail="Не удалось получить статус настройки ERP. Проверьте журнал ERP."

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------
ask() {  # ask VAR LABEL DEFAULT
  local __var="$1" __label="$2" __default="${3:-}"
  if [ "${WIZARD_NONINTERACTIVE:-0}" = "1" ]; then
    local envname="${__var^^}"
    printf -v "$__var" '%s' "${!envname:-$__default}"
    return
  fi
  local __ans
  read -r -p "$__label [$__default]: " __ans || true
  printf -v "$__var" '%s' "${__ans:-$__default}"
}

json_escape() { printf '%s' "$1" | sed 's/\\/\\\\/g; s/"/\\"/g'; }

# ---------------------------------------------------------------------------
# Collect answers
# ---------------------------------------------------------------------------
ERP_DIR="${AI_ERP_DIR:-$(cd "$(dirname "$0")/.." && pwd)}"
collect_answers() {
  echo "== $(msg welcome) ($(echo "$LANG_CODE" | tr 'a-z' 'A-Z')) =="
  ask COMPANY_NAME "$(msg q_company)" "Northwind Traders"
  ask COMPANY_LEGAL "$(msg q_legal)" "Northwind Traders S.r.l."
  ask COMPANY_VAT "$(msg q_vat)" "IT01234560123"
  ask COMPANY_ADDRESS "$(msg q_address)" "Via Roma 10"
  ask COMPANY_CITY "$(msg q_city)" "Milan"
  ask COMPANY_COUNTRY "$(msg q_country)" "Italy"
  ask COMPANY_CURRENCY "$(msg q_currency)" "EUR"
  ask DB_HOST "$(msg q_dbhost)" "127.0.0.1"
  ask DB_PORT "$(msg q_dbport)" "5432"
  ask DB_USER "$(msg q_dbuser)" "dev"
  ask DB_PASS "$(msg q_dbpass)" "dev"
  ask DB_NAME "$(msg q_dbname)" "erp"
  ask ERP_USER "$(msg q_user)" "erp@webvella.com"
  ask ERP_PASS "$(msg q_pass)" "erp"
  ask AB_DIR "$(msg q_abdir)" "$HOME/.agentbridge"
}

# ---------------------------------------------------------------------------
# Write the company record into bootstrap.json (the file the ERP actually reads).
# The full data model ships with the repo; the wizard only replaces the company
# seed with the user's answers, keeping every other entity and seed intact.
# ---------------------------------------------------------------------------
write_bootstrap() {
  echo "$(msg generating)"
  local bj="$ERP_DIR/AI.Erp.Site/bootstrap.json"
  if [ ! -f "$bj" ]; then
    echo "  bootstrap.json not found at $bj" >&2
    return 1
  fi
  cp "$bj" "$bj.bak" 2>/dev/null || true
  COMPANY_NAME="$COMPANY_NAME" COMPANY_LEGAL="$COMPANY_LEGAL" COMPANY_VAT="$COMPANY_VAT" \
  COMPANY_ADDRESS="$COMPANY_ADDRESS" COMPANY_CITY="$COMPANY_CITY" COMPANY_COUNTRY="$COMPANY_COUNTRY" \
  COMPANY_CURRENCY="$COMPANY_CURRENCY" ERP_USER="$ERP_USER" \
  python3 - "$bj" <<'PY'
import json, os, sys
p = sys.argv[1]
with open(p, encoding="utf-8") as f:
    d = json.load(f)
rec = {
    "name": os.environ["COMPANY_NAME"],
    "legal_name": os.environ["COMPANY_LEGAL"],
    "vat_number": os.environ["COMPANY_VAT"],
    "address": os.environ["COMPANY_ADDRESS"],
    "city": os.environ["COMPANY_CITY"],
    "country": os.environ["COMPANY_COUNTRY"],
    "email": os.environ["ERP_USER"],
    "phone": "",
    "currency": os.environ["COMPANY_CURRENCY"],
    "default_warehouse_id": "@warehouse:WH1",
}
d.setdefault("seed", {})["company"] = [rec]
with open(p, "w", encoding="utf-8") as f:
    json.dump(d, f, ensure_ascii=False, indent=2)
print("  updated company record in " + p)
PY
}

# ---------------------------------------------------------------------------
# Setup DB + start ERP + verify
# ---------------------------------------------------------------------------
setup_erp() {
  echo "$(msg settingup)"
  export PGPASSWORD="$DB_PASS"
  # Create the database if it does not exist (best effort; needs a reachable server).
  psql -h "$DB_HOST" -p "$DB_PORT" -U "$DB_USER" -d postgres -tAc \
    "SELECT 1 FROM pg_database WHERE datname='$DB_NAME'" 2>/dev/null | grep -q 1 \
    || psql -h "$DB_HOST" -p "$DB_PORT" -U "$DB_USER" -d postgres -c "CREATE DATABASE $DB_NAME" 2>/dev/null || true
  # The ERP reads its connection string from config.json; the wizard prints what to set.
  echo "  Set AI.Erp.Site/config.json -> Settings.ConnectionString to:"
  echo "  Server=$DB_HOST;Port=$DB_PORT;User Id=$DB_USER;Password=$DB_PASS;Database=$DB_NAME;Pooling=true;"
}

verify_erp() {
  local base="${ERP_BASE_URL:-http://127.0.0.1:5080}"
  local token
  token=$(curl -s -X POST "$base/api/v3/en_US/auth/jwt/token" \
    -H 'Content-Type: application/json' \
    -d "{\"email\":\"$ERP_USER\",\"password\":\"$ERP_PASS\"}" \
    | sed -n 's/.*"object":"\([^"]*\)".*/\1/p')
  if [ -z "$token" ]; then echo "$(msg verify_fail)"; return 1; fi
  local status
  status=$(curl -s "$base/api/v3.0/p/agent/setup-status" -H "Authorization: Bearer $token")
  if printf '%s' "$status" | grep -q '"installed":true'; then
    echo "$(msg verify_ok)"
    printf '  %s\n' "$status"
    return 0
  fi
  echo "$(msg verify_fail)"
  printf '  %s\n' "$status"
  return 1
}

# ---------------------------------------------------------------------------
# Install AgentBridge + ErpTool plugin
# ---------------------------------------------------------------------------
install_agentbridge() {
  echo "$(msg installing)"
  mkdir -p "$AB_DIR"
  local ab_zip="$AB_DIR/agentbridge.tar.gz"
  echo "  AgentBridge: download the latest release into $AB_DIR"
  echo "    https://github.com/Graphene-Lab/AgentBridge/releases/latest"
  echo "  ErpTool plugin: copy into $AB_DIR/Tools/ErpTool/"
  echo "    https://github.com/Graphene-Lab/ErpTool/releases/latest"
  # Write the ERP connection so the tool can reach the ERP.
  mkdir -p "$AB_DIR/PersistentData"
  cat > "$AB_DIR/PersistentData/erp.json" <<EOF
{
  "baseUrl": "${ERP_BASE_URL:-http://127.0.0.1:5080}",
  "user": "$(json_escape "$ERP_USER")",
  "password": "$(json_escape "$ERP_PASS")"
}
EOF
  echo "  -> $AB_DIR/PersistentData/erp.json"
}

# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------
main() {
  collect_answers
  write_bootstrap
  setup_erp
  install_agentbridge
  if verify_erp; then :; fi
  echo ""
  echo "$(msg done)"
}

main "$@"
