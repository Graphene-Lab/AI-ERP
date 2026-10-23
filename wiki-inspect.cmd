@echo off
setlocal
set "GITDIR=C:\Users\andre\OneDrive\Sorgenti\AI-ERP\.git"
set "WIKI=https://github.com/Graphene-Lab/AI-ERP.wiki.git"
echo === fetch live wiki ===
git --git-dir="%GITDIR%" fetch -f %WIKI% master:refs/wikilive/master 2>&1
echo FETCH_EXIT=%ERRORLEVEL%
echo === files in live wiki ===
git --git-dir="%GITDIR%" ls-tree -r --name-only refs/wikilive/master 2>&1
