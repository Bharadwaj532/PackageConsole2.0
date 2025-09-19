# Test Cases — Edit Existing INI

1) Load Existing
- Steps: Choose an existing package; open editor
- Verify: All sections/keys load; diffs vs original highlighted if applicable

2) Edit & Save
- Steps: Change multiple keys; save
- Verify: Only changed keys written; re-open shows changes

3) Undo/Redo & Delete
- Steps: Make edits; delete a key in Delete Key Mode; undo
- Verify: Behaviors consistent with INI Console rules

4) Validation Errors
- Steps: Introduce invalid value; attempt save
- Verify: Inline validation blocks save; message clear

5) Concurrency
- Steps: Open same INI in two app instances; edit in one; refresh in the other
- Verify: No silent overwrite; clear message to reload

