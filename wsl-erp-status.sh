#!/usr/bin/env bash
echo "=== ERP process ==="
pgrep -af "AI.Erp.Site.dll" || echo "ERP NOT running"
echo "=== port 5080 ==="
(curl -s -o /dev/null -w '%{http_code}\n' --max-time 5 http://127.0.0.1:5080/ || echo "no response")
