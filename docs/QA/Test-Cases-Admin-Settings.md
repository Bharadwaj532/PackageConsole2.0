# Test Cases — Admin Settings

1) Access Control
- Steps: Open Admin Settings as Standard user
- Verify: Access blocked/hidden

2) Paths Configuration
- Steps: Set BasePath, Central/Metadata paths to QA folders; Save
- Verify: Persisted; reopening shows values

3) tooltips.json Editing
- Steps: Add/update a tooltip; save
- Verify: Appears in INI Console tooltips; survives app restart

4) Central Mirroring / Master Refresh
- Steps: Trigger master refresh or mirror (as applicable)
- Verify: Local build → copy to Central succeeds; Dashboard reflects updates

5) Error Handling
- Steps: Enter invalid path; save
- Verify: Validation prevents save; helpful message

