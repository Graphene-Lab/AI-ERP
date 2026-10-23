#!/usr/bin/env bash
# Start the ERP (already built in WSL), wait, then re-run the wizard test.
cd /mnt/c/Users/andre/OneDrive/Sorgenti/AI-ERP/AI.Erp.Site || exit 1
pkill -f "AI.Erp.Site.dll" 2>/dev/null
sleep 2
nohup dotnet bin/Release/net10.0/AI.Erp.Site.dll --urls http://127.0.0.1:5080 > /tmp/erp.log 2>&1 &
echo "started pid $!, waiting for bootstrap..."
sleep 24
bash /mnt/c/Users/andre/OneDrive/Sorgenti/AI-ERP/wsl-wizard-test.sh
