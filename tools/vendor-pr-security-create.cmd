@echo off
setlocal
cd /d "C:\Users\andre\OneDrive\Sorgenti\AI-ERP"
gh pr create --repo WebVella/WebVella-ERP --base master --head Graphene-Lab:security/code-compile-and-ckeditor --title "security: two fixes in WebApiController (code-compile access, CKEditor XSS)" --body-file ".git\vendor-pr-security-body.md"
echo GH_EXIT=%ERRORLEVEL%
endlocal
