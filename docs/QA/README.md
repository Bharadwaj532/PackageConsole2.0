# Quality Assurance Pack — PackageConsole (WPF .NET 8)

This folder contains all documents to execute formal QA on PackageConsole. Start here.

Contents
- Test-Plan.md (scope, strategy, entry/exit criteria)
- Environment-Setup.md (how to prepare a clean test rig)
- Smoke-Checklist.md (15-minute gate)
- Test-Cases-*.md (feature-level detailed steps)
- Regression-Suite.md (stable always-run set)
- UAT-Script.md (business validation script)
- Bug-Report-Template.md (defect format)
- Release-Readiness-Checklist.md (go/no-go)
- Accessibility-Checklist.md, Security-Privacy-Checklist.md
- Performance-Test-Plan.md
- DB-Integrity-Checklist.md
- Test-Data/ (sample data guidance)

How to run the app (Debug)
1) Close any running instance
2) Build: `dotnet build -c Debug`
3) Launch: `bin/Debug/net8.0-windows/PackageConsole.exe`

Test identities
- Admin tester: account with Admin Mode access
- Standard tester: account without admin privileges

Key areas to validate
- INI Console (primary): editing UX, undo/redo, delete mode, permissions editors, config toggles, validation
- Package Dashboard: filters, Submitted By dropdown, Refresh Master, CSV export
- Add Package: creation, upgrade section numbering (UPGRADE1 → UPGRADE2 …)
- Edit Existing INI: load, edit, save, diff behavior
- Admin Settings: paths, tooltips.json editing, central mirroring
- Feedback: submit/read
- SQLite over UNC: reliability via local temp copy pattern

Start with Smoke-Checklist.md, then follow Test-Plan.md.

