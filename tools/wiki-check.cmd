@echo off
setlocal
cd /d "C:\Users\andre\OneDrive\Sorgenti\AI-ERP"
git ls-remote https://github.com/Graphene-Lab/AI-ERP.wiki.git
echo LSREMOTE_EXIT=%ERRORLEVEL%
endlocal
