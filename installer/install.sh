#!/usr/bin/env bash
#
# AI ERP - one-shot installer (Linux native + macOS via Docker)
#
# Prepares a complete, working machine in a single run:
#   1. Asks for the company's anagrafic data and the AI provider.
#   2. Installs PostgreSQL 16 if it is not already present.
#   3. Downloads the AI ERP, AgentBridge and ErpTool release archives.
#   4. Configures the ERP (connection string, JWT, company seed).
#   5. Configures AgentBridge with the chosen LLM provider and the ErpTool plugin.
#   6. Installs the ERP as a PWA with a desktop + launcher icon.
#   7. Opens the ERP.
#
# Release archives are downloaded from GitHub releases:
#   AI ERP        https://github.com/Graphene-Lab/AI-ERP/releases
#   AgentBridge   https://github.com/Graphene-Lab/AgentBridge/releases
#   ErpTool       https://github.com/Graphene-Lab/ErpTool/releases
#
set -euo pipefail

# ---------------------------------------------------------------------------
# Constants
# ---------------------------------------------------------------------------
ERP_REPO="Graphene-Lab/AI-ERP"
AB_REPO="Graphene-Lab/AgentBridge"
TOOL_REPO="Graphene-Lab/ErpTool"

ERP_PORT="${ERP_PORT:-5080}"
AB_PORT="${AB_PORT:-5290}"
ERP_URL="http://127.0.0.1:${ERP_PORT}"

DB_NAME="aierp"
DB_USER="aierp"

# The ERP auto-creates this default administrator on first run; the agent logs in
# with it. The user is told to change the password after the first login.
ERP_ADMIN_EMAIL="erp@webvella.com"
ERP_ADMIN_PASSWORD="erp"

INSTALL_ROOT="${INSTALL_ROOT:-$HOME/.aierp}"
ERP_DIR="$INSTALL_ROOT/erp"
AB_DIR="$INSTALL_ROOT/agentbridge"
LOG_DIR="$INSTALL_ROOT/logs"

# Release tags. Pinned for reproducibility; override with env vars.
ERP_TAG="${ERP_TAG:-v1.26.09.22}"
AB_TAG="${AB_TAG:-v1.26.09.19}"
TOOL_TAG="${TOOL_TAG:-v1.26.09.11}"

OS="$(uname -s)"
ARCH="$(uname -m)"

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------
log()  { printf '\033[1;34m==>\033[0m %s\n' "$*"; }
warn() { printf '\033[1;33m[!]\033[0m %s\n' "$*"; }
err()  { printf '\033[1;31m[x]\033[0m %s\n' "$*" >&2; }
die()  { err "$*"; exit 1; }

ask() { # ask <var> <prompt> [default]
	local __var="$1" __prompt="$2" __def="${3:-}" __in
	if [ -n "$__def" ]; then read -r -p "$__prompt [$__def]: " __in || __in=""; else read -r -p "$__prompt: " __in || __in=""; fi
	printf -v "$__var" '%s' "${__in:-$__def}"
}

secret() { # secret <var> <prompt>
	local __var="$1" __prompt="$2" __in
	read -r -s -p "$__prompt: " __in || __in=""; echo
	printf -v "$__var" '%s' "$__in"
}

gen_password() { openssl rand -hex 16 2>/dev/null || head -c 32 /dev/urandom | od -An -tx1 | tr -d ' \n'; }
gen_key()      { openssl rand -hex 48 2>/dev/null || head -c 96 /dev/urandom | od -An -tx1 | tr -d ' \n'; }
need_cmd() { command -v "$1" >/dev/null 2>&1; }

have_sudo() {
	if [ "$(id -u)" -eq 0 ]; then return 0; fi
	if need_cmd sudo && sudo -n true 2>/dev/null; then return 0; fi
	if need_cmd sudo; then
		log "Administrator (root/sudo) privileges are required to install PostgreSQL."
		log "You may be asked for your password."
		sudo -v && return 0
	fi
	return 1
}

# ---------------------------------------------------------------------------
# Provider presets.
# provider_spec <id> -> "Protocol|BaseAddress|EndPoint|DefaultModel"
# Protocol is one of: OpenAI | Gemini | Anthropic (the only three AgentBridge speaks).
# Full request URL = BaseAddress + EndPoint (Gemini embeds the model in BaseAddress).
# ---------------------------------------------------------------------------
provider_spec() {
	case "$1" in
		openai)     echo "OpenAI|https://api.openai.com/|v1/chat/completions|gpt-4o-mini" ;;
		anthropic)  echo "Anthropic|https://api.anthropic.com/|v1/messages|claude-3-5-sonnet-latest" ;;
		google)     echo "Gemini|https://generativelanguage.googleapis.com/v1beta/models/__MODEL__:generateContent||gemini-1.5-flash" ;;
		mistral)    echo "OpenAI|https://api.mistral.ai/|v1/chat/completions|mistral-large-latest" ;;
		xai)        echo "OpenAI|https://api.x.ai/|v1/chat/completions|grok-2-latest" ;;
		deepseek)   echo "OpenAI|https://api.deepseek.com/|v1/chat/completions|deepseek-chat" ;;
		perplexity) echo "OpenAI|https://api.perplexity.ai/|chat/completions|llama-3.1-sonar-large-128k-online" ;;
		together)   echo "OpenAI|https://api.together.xyz/|v1/chat/completions|meta-llama/Llama-3.3-70B-Instruct-Turbo" ;;
		meta)       echo "OpenAI|https://api.llama.com/compat/v1/|chat/completions|llama-3.3-70b-versatile" ;;
		cohere)     echo "OpenAI|https://api.cohere.com/compatibility/v1/|chat/completions|command-r-plus" ;;
		zai)        echo "OpenAI|https://api.z.ai/|api/paas/v4/chat/completions|glm-4-plus" ;;
		ollama)     echo "OpenAI|http://localhost:11434/|v1/chat/completions|llama3.1" ;;
		*)          echo "" ;;
	esac
}

# ---------------------------------------------------------------------------
# Wizard
# ---------------------------------------------------------------------------
run_wizard() {
	echo
	log "AI ERP setup - company details"
	echo "Stored in the ERP and used on documents. Press Enter to keep a default."
	echo
	ask COMPANY_NAME   "Company name (ragione sociale)"
	[ -n "$COMPANY_NAME" ] || die "Company name is required."
	ask LEGAL_NAME     "Legal name"                 "$COMPANY_NAME"
	ask VAT_NUMBER     "VAT number (Partita IVA)"
	ask TAX_CODE       "Tax code (Codice Fiscale)"  ""
	ask ADDRESS        "Street address"
	ask CITY           "City"
	ask POSTAL_CODE    "Postal code"
	ask COUNTRY        "Country"                    "Italy"
	ask EMAIL          "Email"
	ask PEC            "Certified email (PEC)"      ""
	ask PHONE          "Phone"
	ask IBAN           "IBAN"                     ""
	ask BANK           "Bank name"                 ""
	ask CURRENCY       "Currency"                  "EUR"
	ask VAT_REGIME     "VAT regime"                "ordinary"
	TIMEZONE="$( { timedatectl show -p Timezone --value 2>/dev/null || date +%Z; } | tr -d '[:space:]' )"
	ask TIMEZONE       "Timezone"                  "${TIMEZONE:-UTC}"

	echo
	log "AI provider (powers the assistant)"
	echo "Pick one: openai, anthropic, google, mistral, cohere, meta, xai,"
	echo "          deepseek, perplexity, together, zai, ollama, custom"
	ask PROVIDER       "Provider"                  "deepseek"
	# bash 3.2 (macOS default) has no ${var,,}; use tr for portability.
	PROVIDER="$(printf '%s' "$PROVIDER" | tr '[:upper:]' '[:lower:]')"
	if [ "$PROVIDER" = "custom" ]; then
		ask P_PROTOCOL "Protocol (OpenAI / Gemini / Anthropic)" "OpenAI"
		ask P_BASE     "API base URL (ends with /)"
		ask P_ENDPOINT "Endpoint path" "v1/chat/completions"
		ask MODEL      "Model name"
	else
		local spec; spec="$(provider_spec "$PROVIDER")"
		[ -n "$spec" ] || die "Unknown provider '$PROVIDER'."
		P_PROTOCOL="${spec%%|*}"; local rest="${spec#*|}"
		P_BASE="${rest%%|*}"; rest="${rest#*|}"
		P_ENDPOINT="${rest%%|*}"; local defmodel="${rest#*|}"
		ask MODEL "Model" "$defmodel"
	fi
	secret PROVIDER_KEY "API key for $PROVIDER"
	[ -n "$PROVIDER_KEY" ] || die "An API key is required for the assistant."
}

# ---------------------------------------------------------------------------
# PostgreSQL (Linux native)
# ---------------------------------------------------------------------------
install_postgres_linux() {
	if [ "${SKIP_PG_INSTALL:-0}" = "1" ]; then
		log "SKIP_PG_INSTALL=1: using the existing PostgreSQL server."; return 0
	fi
	log "Checking PostgreSQL 16..."
	if need_cmd psql && psql --version 2>/dev/null | grep -q ' 16\.'; then
		log "PostgreSQL 16 already installed."; return 0
	fi
	have_sudo || die "Cannot obtain sudo; install PostgreSQL 16 manually."
	if need_cmd apt-get; then
		log "Installing PostgreSQL 16 via apt (PGDG)..."
		sudo apt-get update -y
		sudo apt-get install -y curl ca-certificates gnupg
		curl -fsSL https://www.postgresql.org/media/keys/ACCC4CF8.asc | sudo gpg --dearmor -o /usr/share/keyrings/postgresql.gpg
		echo "deb [signed-by=/usr/share/keyrings/postgresql.gpg] https://apt.postgresql.org/pub/repos/apt $(. /etc/os-release && echo "$VERSION_CODENAME")-pgdg main" | sudo tee /etc/apt/sources.list.d/pgdg.list >/dev/null
		sudo apt-get update -y
		sudo apt-get install -y postgresql-16
	elif need_cmd dnf; then
		log "Installing PostgreSQL 16 via dnf..."
		sudo dnf install -y "https://download.postgresql.org/pub/repos/yum/reporpms/EL-$(rpm -E %{rhel})-x86_64/pgdg-redhat-repo-latest.noarch.rpm" || true
		sudo dnf install -y postgresql16-server postgresql16
		sudo /usr/pgsql-16/bin/postgresql-16-setup initdb || true
	else
		die "No supported package manager (apt/dnf). Install PostgreSQL 16 manually."
	fi
	log "PostgreSQL 16 installed."
}

ensure_db_linux() {
	log "Creating database '$DB_NAME' and user '$DB_USER'..."
	have_sudo || die "Cannot obtain sudo to configure PostgreSQL."
	# The ERP creates casts between the built-in text/uuid types on first run, which
	# requires a superuser role, so the app role is created as SUPERUSER.
	sudo -u postgres psql -tAc "SELECT 1 FROM pg_roles WHERE rolname='${DB_USER}'" | grep -q 1 \
		|| sudo -u postgres psql -c "CREATE ROLE ${DB_USER} LOGIN SUPERUSER PASSWORD '${DB_PASSWORD}';" >/dev/null
	sudo -u postgres psql -tAc "SELECT 1 FROM pg_database WHERE datname='${DB_NAME}'" | grep -q 1 \
		|| sudo -u postgres createdb -O "${DB_USER}" "${DB_NAME}"
	log "Database ready."
}

ensure_jq() {
	if need_cmd jq; then return 0; fi
	log "Installing jq (needed to set the company data)..."
	if need_cmd apt-get; then sudo apt-get install -y jq >/dev/null 2>&1
	elif need_cmd dnf; then sudo dnf install -y jq >/dev/null 2>&1; fi
	need_cmd jq || warn "jq could not be installed; the company seed will keep the shipped sample."
}

# ---------------------------------------------------------------------------
# Download + extract
# ---------------------------------------------------------------------------
download_extract() { # download_extract <repo> <tag> <asset> <dest>
	local repo="$1" tag="$2" asset="$3" dest="$4"
	local url="https://github.com/${repo}/releases/download/${tag}/${asset}"
	if [ -d "$dest" ] && [ -n "$(ls -A "$dest" 2>/dev/null)" ]; then
		log "Skipping $asset; $dest is already populated."
		return 0
	fi
	log "Downloading ${asset} ..."
	mkdir -p "$dest"
	local tmp="$INSTALL_ROOT/${asset}"
	curl -fL --retry 3 -o "$tmp" "$url" || die "Download failed: $url"
	if tar -tzf "$tmp" >/dev/null 2>&1; then tar -xzf "$tmp" -C "$dest"; else unzip -o "$tmp" -d "$dest" >/dev/null; fi
	rm -f "$tmp"
	log "Extracted to $dest"
}

# ---------------------------------------------------------------------------
# ERP configuration
# ---------------------------------------------------------------------------
write_erp_config() {
	log "Writing ERP config.json ..."
	local conn="Server=localhost;Port=5432;User Id=${DB_USER};Password=${DB_PASSWORD};Database=${DB_NAME};Pooling=true;MinPoolSize=1;MaxPoolSize=100;CommandTimeout=120;Timeout=120;KeepAlive=120;"
	local jwtkey enckey; jwtkey="$(gen_key)"; enckey="$(gen_key)"
	# Use jq so values with quotes/backslashes (company name, password) are escaped.
	if need_cmd jq; then
		jq -n --arg conn "$conn" --arg enc "$enckey" --arg tz "$TIMEZONE" \
		      --arg app "$COMPANY_NAME" --arg jwt "$jwtkey" \
		'{Settings:{ConnectionString:$conn,EncryptionKey:$enc,Lang:"en",Locale:"en-US",TimeZoneName:$tz,CacheKey:"",DevelopmentMode:"false",EnableBackgroundJobs:"true",EnableFileSystemStorage:"false",EmailEnabled:false,AppName:$app,NavLogoUrl:"",SystemMasterBackgroundImageUrl:"",Jwt:{Key:$jwt,Issuer:"ai-erp",Audience:"ai-erp"}}}' \
			> "$ERP_DIR/config.json"
		return 0
	fi
	warn "jq not found; writing config.json raw (company name/password must contain no quotes or backslashes)."
	cat > "$ERP_DIR/config.json" <<EOF
{
  "Settings": {
    "ConnectionString": "${conn}",
    "EncryptionKey": "${enckey}",
    "Lang": "en",
    "Locale": "en-US",
    "TimeZoneName": "${TIMEZONE}",
    "CacheKey": "",
    "DevelopmentMode": "false",
    "EnableBackgroundJobs": "true",
    "EnableFileSystemStorage": "false",
    "EmailEnabled": false,
    "AppName": "${COMPANY_NAME}",
    "NavLogoUrl": "",
    "SystemMasterBackgroundImageUrl": "",
    "Jwt": { "Key": "${jwtkey}", "Issuer": "ai-erp", "Audience": "ai-erp" }
  }
}
EOF
}

customize_bootstrap() {
	log "Customizing the company seed in bootstrap.json ..."
	local bf="$ERP_DIR/bootstrap.json"
	[ -f "$bf" ] || die "bootstrap.json not found in the ERP archive."
	if ! need_cmd jq; then
		warn "jq not found; keeping the shipped sample company. Install jq to set your company."
		return 0
	fi
	local esc_name esc_legal esc_vat esc_addr esc_city esc_country esc_email esc_phone esc_cur
	esc_name="$(printf '%s' "$COMPANY_NAME" | jq -Rs .)"
	esc_legal="$(printf '%s' "$LEGAL_NAME" | jq -Rs .)"
	esc_vat="$(printf '%s' "$VAT_NUMBER" | jq -Rs .)"
	esc_addr="$(printf '%s\n%s, %s %s' "$ADDRESS" "$POSTAL_CODE" "$CITY" "$COUNTRY" | jq -Rs .)"
	esc_city="$(printf '%s' "$CITY" | jq -Rs .)"
	esc_country="$(printf '%s' "$COUNTRY" | jq -Rs .)"
	esc_email="$(printf '%s' "$EMAIL" | jq -Rs .)"
	esc_phone="$(printf '%s' "$PHONE" | jq -Rs .)"
	esc_cur="$(printf '%s' "$CURRENCY" | jq -Rs .)"
	local tmp="$bf.tmp"
	jq --argjson name "$esc_name" --argjson legal "$esc_legal" --argjson vat "$esc_vat" \
	   --argjson addr "$esc_addr" --argjson city "$esc_city" --argjson country "$esc_country" \
	   --argjson email "$esc_email" --argjson phone "$esc_phone" --argjson cur "$esc_cur" \
	   '.seed.company = [ {
			name: $name, legal_name: $legal, vat_number: $vat, address: $addr,
			city: $city, country: $country, email: $email, phone: $phone,
			currency: $cur, default_warehouse_id: "@warehouse:WH1"
	   } ]' "$bf" > "$tmp" && mv "$tmp" "$bf"
	log "Company seed set to: $COMPANY_NAME"
}

# ---------------------------------------------------------------------------
# AgentBridge configuration
# ---------------------------------------------------------------------------
configure_agentbridge() {
	log "Configuring AgentBridge (provider=$PROVIDER, protocol=$P_PROTOCOL) ..."

	# Provider file: <AB_DIR>/PersistentData/providers.json (array; IsDefault wins).
	local pd="$AB_DIR/PersistentData"
	mkdir -p "$pd"
	local gemini_base="$P_BASE"
	if [ "$P_PROTOCOL" = "Gemini" ]; then
		gemini_base="${P_BASE//__MODEL__/$MODEL}"
	fi
	if need_cmd jq; then
		jq -n --arg name "$PROVIDER" --arg proto "$P_PROTOCOL" --arg model "$MODEL" \
		      --arg key "$PROVIDER_KEY" --arg addr "$gemini_base" --arg ep "$P_ENDPOINT" \
		  '[{
			ProviderName: $name,
			IsDefault: true,
			Protocol: $proto,
			CacheType: "PrefixCache",
			ModelName: $model,
			ApiKey: $key,
			BaseAddress: $addr,
			EndPoint: $ep,
			Timeout: "00:01:00",
			PauseBetweenRequests: "00:00:00",
			ContextWindow: 128000
		  }]' > "$pd/providers.json"
	else
		warn "jq not found; writing a minimal providers.json without jq."
		printf '[{"ProviderName":"%s","IsDefault":true,"Protocol":"%s","ModelName":"%s","ApiKey":"%s","BaseAddress":"%s","EndPoint":"%s"}]\n' \
			"$PROVIDER" "$P_PROTOCOL" "$MODEL" "$PROVIDER_KEY" "$gemini_base" "$P_ENDPOINT" > "$pd/providers.json"
	fi

	# ErpTool connection fallback file (camelCase).
	jq -n --arg b "$ERP_URL" --arg u "$ERP_ADMIN_EMAIL" --arg p "$ERP_ADMIN_PASSWORD" \
		'{baseUrl:$b, user:$u, password:$p}' > "$pd/erp.json" 2>/dev/null \
		|| printf '{"baseUrl":"%s","user":"%s","password":"%s"}\n' "$ERP_URL" "$ERP_ADMIN_EMAIL" "$ERP_ADMIN_PASSWORD" > "$pd/erp.json"

	# Place the ErpTool plugin where the host scans: <AB_DIR>/Tools/ErpTool/
	local tool_src="$INSTALL_ROOT/erptool"
	if [ -d "$tool_src" ]; then
		mkdir -p "$AB_DIR/Tools/ErpTool"
		cp -rf "$tool_src"/. "$AB_DIR/Tools/ErpTool"/
		log "ErpTool plugin installed at $AB_DIR/Tools/ErpTool"
	fi
}

# ---------------------------------------------------------------------------
# Services
# ---------------------------------------------------------------------------
start_services_linux() {
	log "Starting ERP and AgentBridge ..."
	mkdir -p "$LOG_DIR"
	if [ -x "$ERP_DIR/AI.Erp.Site" ]; then
		( cd "$ERP_DIR" && setsid ./AI.Erp.Site --urls "$ERP_URL" >"$LOG_DIR/erp.log" 2>&1 & echo $! > "$INSTALL_ROOT/erp.pid" )
	else
		( cd "$ERP_DIR" && setsid dotnet AI.Erp.Site.dll --urls "$ERP_URL" >"$LOG_DIR/erp.log" 2>&1 & echo $! > "$INSTALL_ROOT/erp.pid" )
	fi
	log "ERP started (pid $(cat "$INSTALL_ROOT/erp.pid")). Waiting for first-run setup..."
	wait_for_erp
	if [ -x "$AB_DIR/agent" ] || [ -f "$AB_DIR/agent.dll" ]; then
		local abexe="$AB_DIR/agent"
		[ -x "$abexe" ] || abexe="dotnet $AB_DIR/agent.dll"
		( cd "$AB_DIR" && ERP_BASE_URL="$ERP_URL" ERP_USER="$ERP_ADMIN_EMAIL" ERP_PASSWORD="$ERP_ADMIN_PASSWORD" \
			setsid $abexe >"$LOG_DIR/agentbridge.log" 2>&1 & echo $! > "$INSTALL_ROOT/ab.pid" )
		log "AgentBridge started (pid $(cat "$INSTALL_ROOT/ab.pid" 2>/dev/null || echo '?'))."
	fi
}

wait_for_erp() {
	local i
	for i in $(seq 1 60); do
		if curl -fs "$ERP_URL/manifest.webmanifest" >/dev/null 2>&1; then log "ERP is up."; return 0; fi
		sleep 2
	done
	warn "ERP did not respond within 120s; check $LOG_DIR/erp.log"
}

# ---------------------------------------------------------------------------
# PWA launcher (desktop + launcher icon)
# ---------------------------------------------------------------------------
install_pwa_launcher_linux() {
	log "Installing the ERP launcher (PWA app window) ..."
	local icon="$ERP_DIR/wwwroot/assets/pwa-512x512.png"
	local browser=""
	for b in google-chrome google-chrome-stable chromium chromium-browser microsoft-edge; do
		if need_cmd "$b"; then browser="$b"; break; fi
	done
	[ -n "$browser" ] || warn "No Chrome/Chromium/Edge found; the PWA install prompt needs one."

	local desktop_file="$HOME/.local/share/applications/aierp.desktop"
	mkdir -p "$HOME/.local/share/applications"
	if [ -n "$browser" ]; then
		cat > "$desktop_file" <<EOF
[Desktop Entry]
Type=Application
Name=AI ERP
Comment=${COMPANY_NAME} - AI ERP
Exec=${browser} --app=${ERP_URL}/?pwa=1
Icon=${icon}
Terminal=false
Categories=Office;
EOF
		chmod +x "$desktop_file"
		need_cmd update-desktop-database && update-desktop-database "$HOME/.local/share/applications" 2>/dev/null || true
		local desk="$HOME/Desktop"
		[ -d "$desk" ] && cp "$desktop_file" "$desk/aierp.desktop" && chmod +x "$desk/aierp.desktop" || true
		log "Launcher installed. Opening the ERP app window..."
		"$browser" --app="${ERP_URL}/?pwa=1" >/dev/null 2>&1 &
	else
		log "Opening the ERP in the default browser..."
		( xdg-open "$ERP_URL" >/dev/null 2>&1 || true )
	fi
}

# ---------------------------------------------------------------------------
# macOS (Docker for PostgreSQL; ERP macOS binary pending)
# ---------------------------------------------------------------------------
install_macos_docker() {
	log "macOS: installing AI ERP via Docker (ERP + PostgreSQL)..."
	need_cmd docker || die "Docker is required on macOS. Install Docker Desktop first."
	mkdir -p "$INSTALL_ROOT"
	local compose="$INSTALL_ROOT/docker-compose.yml"
	cat > "$compose" <<EOF
services:
  db:
    image: postgres:16
    restart: unless-stopped
    environment:
      POSTGRES_DB: ${DB_NAME}
      POSTGRES_USER: ${DB_USER}
      POSTGRES_PASSWORD: ${DB_PASSWORD}
    volumes:
      - aierp_pgdata:/var/lib/postgresql/data
    ports:
      - "127.0.0.1:5432:5432"
volumes:
  aierp_pgdata:
EOF
	log "Starting PostgreSQL container..."
	docker compose -f "$compose" up -d db || die "docker compose failed."
	warn "The AI ERP has no macOS release binary yet (only linux-x64 / win-x64)."
	warn "The ERP app cannot run natively on macOS until a macOS build is published."
	warn "PostgreSQL is ready; point an ERP host (Linux/Docker) at 127.0.0.1:5432."
}

# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------
main() {
	log "AI ERP installer ($OS/$ARCH)"
	mkdir -p "$INSTALL_ROOT"
	DB_PASSWORD="${DB_PASSWORD:-$(gen_password)}"
	run_wizard

	case "$OS" in
		Linux)
			install_postgres_linux
			ensure_db_linux
			ensure_jq
			download_extract "$ERP_REPO" "$ERP_TAG" "aierp-linux-x64.tar.gz" "$ERP_DIR"
			download_extract "$AB_REPO" "$AB_TAG" "agentbridge-linux-x64.tar.gz" "$AB_DIR"
			download_extract "$TOOL_REPO" "$TOOL_TAG" "ErpTool-${TOOL_TAG#v}.zip" "$INSTALL_ROOT/erptool"
			write_erp_config
			customize_bootstrap
			configure_agentbridge
			start_services_linux
			install_pwa_launcher_linux
			;;
		Darwin)
			install_macos_docker
			;;
		*)
			die "Unsupported OS: $OS. Use install.bat on Windows."
			;;
	esac

	echo
	log "Done."
	echo "  ERP URL:        $ERP_URL"
	echo "  DB user/pass:   $DB_USER / $DB_PASSWORD   (in $ERP_DIR/config.json)"
	echo "  Install root:   $INSTALL_ROOT"
	echo
	echo "  ERP administrator (created automatically):"
	echo "    email:    $ERP_ADMIN_EMAIL"
	echo "    password: $ERP_ADMIN_PASSWORD"
	echo "    >>> Change this password immediately after the first login. <<<"
	echo
	echo "  To finish the PWA install, open the ERP in Chrome/Edge and click the"
	echo "  'Install' icon in the address bar (the app icon is already on your desktop)."
}

main "$@"
