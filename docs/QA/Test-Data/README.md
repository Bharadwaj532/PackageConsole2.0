# Test Data Guidance

INI Samples
- Small: 10–20 keys, 3–5 sections
- Large: ≥2,000 keys across ≥50 sections (for performance)
- Include permissions sections (foldperm/regperm/fileperm) with multiple rows

Upgrade INIs
- Prepare two separate upgrade INIs to verify sequential UPGRADE1/UPGRADE2 creation

Master/User DBs
- Create 2–3 user DBs with distinct packages
- Prepare a Master DB baseline; capture expected row counts

Feedback
- Provide 3–5 sample entries with timestamps and users

Storage
- Keep all QA data under a dedicated QA BasePath and Central folder
- Store expected results (CSV/row counts) for quick comparison

