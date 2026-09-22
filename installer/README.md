# One-shot installer

These files set up the whole AI ERP ecosystem (PostgreSQL 16, the ERP, AgentBridge and the
ErpTool plugin) in one run.

- `install.bat` — Windows launcher. Double-click it; it runs `install.ps1`.
- `install.ps1` — the actual Windows installer.
- `install.sh` — the Linux / macOS installer.

The full guide is in [First-run setup](../docs/FIRST-RUN-SETUP.md).

## Windows: SmartScreen / Smart App Control warning

After you download `install.bat`, Windows may say the file "is not from a trusted publisher" or
Smart App Control may stop it. This is normal for any script downloaded from the internet that is
not signed with a paid code-signing certificate. It does not mean the file is a virus.

To continue, keep Windows protection on and allow only this file:

- In the blue SmartScreen window, click **More info** (Ulteriori informazioni), then
  **Run anyway** (Esegui comunque).
- Or right-click `install.bat`, choose **Properties**, tick **Unblock** (Sblocca), click
  **Apply**, then run it again.

You do not need to disable SmartScreen or Smart App Control for the whole computer. Disabling it
lowers your protection against everything else.

If you prefer not to keep the script on disk, run it straight from PowerShell instead:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -Command "irm https://raw.githubusercontent.com/Graphene-Lab/AI-ERP/master/installer/install.ps1 | iex"
```

You can read exactly what the installer does before running it in
[`install.ps1`](install.ps1). It only downloads PostgreSQL and the official release archives
from this project's GitHub releases and from the EDB site, writes its configuration under your
local app data folder, and starts the services.
