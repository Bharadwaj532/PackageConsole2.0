# Test Plan — PackageConsole

Objective
Ensure the desktop app is functionally correct, stable, and ready for wider Quality Testing.

Scope
- In scope: UI workflows, SQLite data flows (local, central, master), file I/O (INI, CSV), admin-only features, role behavior, error handling.
- Out of scope: Cloud migration deliverables (separate track), non-Windows platforms.

Test Types
- Smoke testing (gate)
- Functional testing (feature test cases)
- Negative/error handling
- Regression testing
- Role-based testing (Admin vs Standard)
- Accessibility (basic keyboard focus/contrast)
- Security & privacy checks (local/UNC paths, PII)
- Performance (basic responsiveness, large INI)

Test Strategy
- Feature sheets with step-by-step cases (Test-Cases-*.md)
- Stable Regression-Suite.md run on each build
- Dual-identity passes (Admin/Standard)
- Data safety: use test BasePath/Central paths; never production shares

Entry Criteria
- Build succeeds; app launches on test rig
- Help content updated; screenshots available
- Admin paths configured to test locations

Exit Criteria
- 0 open Severity 1 (blocker) defects
- ≤2 open Severity 2 defects with workarounds approved
- All smoke/regression cases pass
- UAT-Script.md signed by requester

Risks & Mitigations
- SQLite on UNC can appear empty → app uses local temp copies; test via DB-Integrity-Checklist.md
- Concurrent edits → validate conflict handling via save validation and undo/redo
- Path misconfig → use Admin Settings and Environment-Setup.md

Deliverables
- Executed checklists with results
- Defect tickets (Bug-Report-Template.md)
- UAT sign-off

Schedule
- Smoke: ~15–30 min
- Full feature pass: ~4–6 hours
- Regression on new build: ~60–90 min

