@echo off
setlocal
cd /d "C:\Users\andre\OneDrive\Sorgenti\AI-ERP"
gh pr create --repo WebVella/WebVella-ERP --base master --head Graphene-Lab:fix/record-readback-permission --title "fix: read back the just-written record ignoring read permissions" --body-file ".git\vendor-pr-rm-body.md"
echo GH_EXIT=%ERRORLEVEL%
endlocal
