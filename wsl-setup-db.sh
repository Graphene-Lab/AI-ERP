#!/usr/bin/env bash
set -e

# Start the PostgreSQL 15 cluster (WSL has no systemd by default).
sudo pg_ctlcluster 15 main start 2>/dev/null || true
sleep 2

# Create the login role used by config.json (dev/dev), idempotent.
if ! sudo -u postgres psql -tAc "SELECT 1 FROM pg_roles WHERE rolname='dev'" | grep -q 1; then
  sudo -u postgres psql -c "CREATE ROLE dev LOGIN PASSWORD 'dev' CREATEDB SUPERUSER;"
fi

# Create the erp database owned by dev, idempotent.
if ! sudo -u postgres psql -tAc "SELECT 1 FROM pg_database WHERE datname='erp'" | grep -q 1; then
  sudo -u postgres createdb -O dev erp
fi

# Allow password auth from localhost for TCP connections (md5/scram).
HBA=/etc/postgresql/15/main/pg_hba.conf
if ! grep -q "host    all             all             127.0.0.1/32" "$HBA"; then
  echo "host    all             all             127.0.0.1/32            md5" | sudo tee -a "$HBA"
fi
sudo pg_ctlcluster 15 main reload

# Verify connectivity as dev over TCP.
PGPASSWORD=dev psql -h 127.0.0.1 -U dev -d erp -tAc "SELECT 'connected as ' || current_user || ' to ' || current_database();"
