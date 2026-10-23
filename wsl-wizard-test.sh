#!/usr/bin/env bash
# Test the setup wizard non-interactively against the running WSL ERP.
export WIZARD_NONINTERACTIVE=1
export WIZARD_LANG=it
export COMPANY_NAME="Test Co"
export COMPANY_LEGAL="Test Co S.r.l."
export COMPANY_VAT="IT999888777"
export COMPANY_ADDRESS="Via Test 1"
export COMPANY_CITY="Roma"
export COMPANY_COUNTRY="Italy"
export COMPANY_CURRENCY="EUR"
export DB_HOST=127.0.0.1
export DB_PORT=5432
export DB_USER=dev
export DB_PASS=dev
export DB_NAME=erp
export ERP_USER=erp@webvella.com
export ERP_PASS=erp
export AB_DIR=/tmp/wizard-test
export ERP_BASE_URL=http://127.0.0.1:5080
rm -rf /tmp/wizard-test
bash /mnt/c/Users/andre/OneDrive/Sorgenti/AI-ERP/tools/setup-wizard.sh
echo "=== company record now in the real bootstrap.json ==="
python3 -c "import json; d=json.load(open('/mnt/c/Users/andre/OneDrive/Sorgenti/AI-ERP/AI.Erp.Site/bootstrap.json')); print(json.dumps(d['seed']['company'], ensure_ascii=False, indent=2))"
echo "=== generated erp.json ==="
cat /tmp/wizard-test/PersistentData/erp.json
