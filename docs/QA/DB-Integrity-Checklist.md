# DB Integrity Checklist (SQLite local/central/master)

Local User DB
- [ ] New package rows appear after Add Package
- [ ] Edits from INI Console persist and reload correctly

Master (All)
- [ ] Refresh Master (Admin) updates Master DB
- [ ] Dashboard counts logical; sampling matches source user DBs

Central/UNC Robustness
- [ ] With central DBs on QA share, data still loads (local temp copy approach)
- [ ] No empty grids when source DBs are populated

CSV Integrity
- [ ] Exported CSV matches current grid including filters

Error Handling
- [ ] Broken path yields clear error and no crash

