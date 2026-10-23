@echo off
setlocal enabledelayedexpansion
cd /d "C:\Users\andre\OneDrive\Sorgenti\AI-ERP"
set "GIT_DIR=.git"
set "GIT_INDEX_FILE=.git\pr-index"
if exist "%GIT_INDEX_FILE%" del "%GIT_INDEX_FILE%"
git read-tree upstream/master || (echo READTREE_FAIL & exit /b 1)
for /f %%b in ('git hash-object -w .vendor-pr-LIBRARIES.md') do set "BLOB=%%b"
echo BLOB=!BLOB!
git update-index --add --cacheinfo 100644,!BLOB!,LIBRARIES.md || (echo UPDATEINDEX_FAIL & exit /b 1)
for /f %%t in ('git write-tree') do set "TREE=%%t"
echo TREE=!TREE!
for /f %%c in ('git commit-tree !TREE! -p upstream/master -m "docs: fill LIBRARIES.md with the third-party libraries used by the project"') do set "COMMIT=%%c"
echo COMMIT=!COMMIT!
git push -f origin !COMMIT!:refs/heads/docs/libraries || (echo PUSH_FAIL & exit /b 1)
echo PUSH_OK
endlocal
