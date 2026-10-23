@echo off
setlocal
cd /d "C:\Users\andre\OneDrive\Sorgenti\AI-ERP"
gh issue create --repo WebVella/WebVella-ERP --title "Any logged-in user can delete any file (DELETE catch-all route has no ownership check)" --body-file ".git\vendor-issue-filedelete-body.md"
echo GH_EXIT=%ERRORLEVEL%
endlocal
