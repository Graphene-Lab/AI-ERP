@echo off
setlocal enabledelayedexpansion
cd /d "C:\Users\andre\OneDrive\Sorgenti\AI-ERP"
set "GIT_DIR=.git"
set "GIT_INDEX_FILE=.git\secpr-index"
set "GIT_AUTHOR_NAME=Andrea Bruno"
set "GIT_AUTHOR_EMAIL=21980406+Andrea-Bruno@users.noreply.github.com"
set "GIT_COMMITTER_NAME=Andrea Bruno"
set "GIT_COMMITTER_EMAIL=21980406+Andrea-Bruno@users.noreply.github.com"
if exist "%GIT_INDEX_FILE%" del "%GIT_INDEX_FILE%"
git read-tree upstream/master || (echo READTREE_FAIL & exit /b 1)
for /f %%b in ('git hash-object -w .git\vendor-webapictrl.cs') do set "BLOB=%%b"
echo BLOB=!BLOB!
git update-index --add --cacheinfo 100644,!BLOB!,WebVella.Erp.Web/Controllers/WebApiController.cs || (echo UPDATEINDEX_FAIL & exit /b 1)
for /f %%t in ('git write-tree') do set "TREE=%%t"
echo TREE=!TREE!
for /f %%c in ('git commit-tree !TREE! -p upstream/master -m "security: require admin for datasource code-compile and fix reflected XSS in CKEditor upload"') do set "COMMIT=%%c"
echo COMMIT=!COMMIT!
git push -f origin !COMMIT!:refs/heads/security/code-compile-and-ckeditor || (echo PUSH_FAIL & exit /b 1)
echo PUSH_OK
endlocal
