@echo off
setlocal
cd /d "C:\Users\andre\OneDrive\Sorgenti\AI-ERP"
git show upstream/master:WebVella.Erp.Web/Controllers/WebApiController.cs > ".git\vendor-webapictrl.cs" 2>nul
if errorlevel 1 (echo SHOW_FAIL & exit /b 1)
echo WROTE
endlocal
