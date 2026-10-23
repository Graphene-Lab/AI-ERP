@echo off
cd /d "C:\Users\andre\OneDrive\Sorgenti\AI-ERP"
set WIZARD_NONINTERACTIVE=1
set WIZARD_LANG=it
set WIZARD_Q_COMPANY=Beta Beta Srl
set WIZARD_Q_LEGAL=Beta Beta S.r.l.
set WIZARD_Q_VAT=IT11122233344
set WIZARD_Q_ADDRESS=Via Beta 9
set WIZARD_Q_CITY=Napoli
set WIZARD_Q_COUNTRY=Italy
set WIZARD_Q_CURRENCY=EUR
set WIZARD_Q_DBHOST=127.0.0.1
set WIZARD_Q_DBPORT=5432
set WIZARD_Q_DBUSER=dev
set WIZARD_Q_DBPASS=dev
set WIZARD_Q_DBNAME=erp
set WIZARD_Q_USER=erp@webvella.com
set WIZARD_Q_PASS=erp
set WIZARD_Q_ABDIR=C:\Users\andre\AppData\Local\Temp\wiz-ps-test
set ERP_BASE_URL=http://127.0.0.1:5080
powershell -NoProfile -ExecutionPolicy Bypass -File "tools\setup-wizard.ps1"
echo === generated erp.json ===
type "C:\Users\andre\AppData\Local\Temp\wiz-ps-test\PersistentData\erp.json"
