# One-shot installer

These files set up the whole AI ERP ecosystem (PostgreSQL 16, the ERP, AgentBridge and the
ErpTool plugin) in one run.

- `install.bat` — Windows launcher. Double-click it; it runs `install.ps1`. If
  `install.ps1` is not next to it (for example when only `install.bat` was downloaded),
  the launcher downloads the installer from GitHub and runs that instead. The window
  stays open at the end so you can read the result.
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

## The desktop icon and "connection refused"

The installer puts an **AI ERP** icon on the Desktop (and in the Start Menu). That icon is a
small launcher: when you double-click it, it first checks whether the ERP is running and, if it
is not, starts it (and the assistant) before opening the app window. This matters because the
ERP is a local web server: if you open the address `http://127.0.0.1:5080` while the server is
not running, the browser shows "This site can't be reached / ERR_CONNECTION_REFUSED". That is
not a proxy or firewall problem — the program simply was not running. After a computer restart,
or if the program was closed, just double-click the **AI ERP** icon and it brings everything
back up.

The launcher writes its logs under the install folder (`%LOCALAPPDATA%\aierp\logs` on Windows,
`~/.aierp/logs` on Linux). If the icon ever shows that the ERP cannot start, look there.
