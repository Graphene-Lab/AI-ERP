#!/usr/bin/env bash
# Fast compile check for the AgentApi plugin (new setup-status / reprovision code).
set +e
cd /mnt/c/Users/andre/OneDrive/Sorgenti/AI-ERP/AI.Erp.Plugins.AgentApi || exit 1
dotnet build -c Release -v q -clp:ErrorsOnly
echo "PLUGIN_BUILD_EXIT=$?"
