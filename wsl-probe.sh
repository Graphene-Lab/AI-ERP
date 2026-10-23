#!/usr/bin/env bash
set +e
cd /mnt/c/Users/andre/OneDrive/Sorgenti/AI-ERP/AI.Erp.Site/bin/Release/net10.0
echo "=== static web asset manifests ==="
ls -la *.staticwebassets*.json 2>&1
echo "=== contentRoot in runtime manifest ==="
for f in *.staticwebassets.runtime.json; do
  [ -f "$f" ] && echo "--- $f ---" && head -c 400 "$f"
done
echo ""
echo "=== endpoints manifest contentRoot ==="
for f in *.staticwebassets.endpoints.json; do
  [ -f "$f" ] && echo "--- $f ---" && head -c 400 "$f"
done
