@echo off
setlocal enabledelayedexpansion
cd /d "C:\Users\andre\OneDrive\Sorgenti\AI-ERP"
set "GIT_DIR=.git"
set "GIT_INDEX_FILE=.git\frspr-index"
set "GIT_AUTHOR_NAME=Andrea Bruno"
set "GIT_AUTHOR_EMAIL=21980406+Andrea-Bruno@users.noreply.github.com"
set "GIT_COMMITTER_NAME=Andrea Bruno"
set "GIT_COMMITTER_EMAIL=21980406+Andrea-Bruno@users.noreply.github.com"
if exist "%GIT_INDEX_FILE%" del "%GIT_INDEX_FILE%"
git read-tree upstream/master || (echo READTREE_FAIL & exit /b 1)
for /f %%b in ('git hash-object -w .git\vendor-securitymanager.cs') do set "SM=%%b"
for /f %%b in ('git hash-object -w .git\vendor-login.cshtml') do set "LH=%%b"
for /f %%b in ('git hash-object -w .git\vendor-login.cshtml.cs') do set "LM=%%b"
echo SM=!SM! LH=!LH! LM=!LM!
git update-index --add --cacheinfo 100644,!SM!,WebVella.Erp/Api/SecurityManager.cs || (echo UPDATEINDEX_FAIL_SM & exit /b 1)
git update-index --add --cacheinfo 100644,!LH!,WebVella.Erp.Web/Pages/login.cshtml || (echo UPDATEINDEX_FAIL_LH & exit /b 1)
git update-index --add --cacheinfo 100644,!LM!,WebVella.Erp.Web/Pages/login.cshtml.cs || (echo UPDATEINDEX_FAIL_LM & exit /b 1)
for /f %%t in ('git write-tree') do set "TREE=%%t"
echo TREE=!TREE!
for /f %%c in ('git commit-tree !TREE! -p upstream/master -m "feat: first-run admin account setup on the login page"') do set "COMMIT=%%c"
echo COMMIT=!COMMIT!
git push -f origin !COMMIT!:refs/heads/feat/first-run-admin-setup || (echo PUSH_FAIL & exit /b 1)
echo PUSH_OK
endlocal
