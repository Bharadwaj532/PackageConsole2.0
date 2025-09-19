# Environment Setup — QA

Prereqs
- Windows 10/11
- .NET SDK 8.x installed
- Test account with Admin rights, and a Standard account

Initial build & run
1) Close any running app instance
2) Build: `dotnet build -c Debug`
3) Run: `bin/Debug/net8.0-windows/PackageConsole.exe`

Configure app for QA
- In Admin Settings:
  - BasePath: set to a QA-only folder (e.g., C:\QA\PackageConsole)
  - Central Share/Metadata path: point to a QA network/test folder (or local folder simulating UNC)
  - tooltips.json: ensure it loads from QA Metadata; save a tiny change to confirm write
- Place sample user SQLite DBs and a MasterDB in QA locations if needed by tests

Artifacts and logs
- Screenshots: capture key flows and failures
- CSV exports: store under QA evidence folder
- Logs: if logging is enabled, attach the log file(s) with time stamps

Resetting state
- Clear QA BasePath subfolders as needed (Backup before clearing)
- Replace sample DBs from a clean baseline

Performance data
- Prepare a large INI sample (≥2,000 keys across sections) for stress tests

