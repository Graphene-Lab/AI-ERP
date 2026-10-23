@echo off
setlocal
set "REPO=C:\Users\andre\OneDrive\Sorgenti\AI-ERP"
set "GITDIR=%REPO%\.git"
set "STAGING=%REPO%\.wiki-staging"
set "WIKI=https://github.com/Graphene-Lab/AI-ERP.wiki.git"
set "GIT_INDEX_FILE=%GITDIR%\wiki-index"
set "GIT_AUTHOR_NAME=Andrea Bruno"
set "GIT_AUTHOR_EMAIL=21980406+Andrea-Bruno@users.noreply.github.com"
set "GIT_COMMITTER_NAME=Andrea Bruno"
set "GIT_COMMITTER_EMAIL=21980406+Andrea-Bruno@users.noreply.github.com"
if exist "%GIT_INDEX_FILE%" del "%GIT_INDEX_FILE%"
git --git-dir="%GITDIR%" --work-tree="%STAGING%" add -A
for /f "delims=" %%t in ('git --git-dir="%GITDIR%" write-tree') do set "TREE=%%t"
for /f "delims=" %%c in ('git --git-dir="%GITDIR%" commit-tree %TREE% -m "docs: add AI ERP wiki"') do set "COMMIT=%%c"
echo TREE=%TREE%
echo COMMIT=%COMMIT%
git --git-dir="%GITDIR%" push -f %WIKI% %COMMIT%:refs/heads/master
echo PUSH_EXIT=%ERRORLEVEL%
