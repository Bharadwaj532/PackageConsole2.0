# Test Cases — INI Console (Primary)

Legend: A=Admin, S=Standard

1) Load & Layout
- Steps: Open INI Console with sample package; observe left/middle/right panes
- Verify: Sections, Keys, Value editor visible; focus moves with selection; tooltips appear on hover
- Role: A,S

2) Browse & Edit w/ Autocomplete
- Steps: Select a key; start typing a known variable; pick from suggestions
- Verify: Autocomplete list shows; chosen token inserts correctly; tooltip text displayed
- Role: A,S

3) Config Gear (⚙️) Toggles
- Steps: Open gear; toggle Show resolved variables, Show tooltips, Expand multi-line editors
- Verify: Resolved preview updates; tooltips visible/hidden; multi-line expands
- Role: A,S

4) Delete Key Mode (safety flow)
- Steps: Enable Delete Key Mode; select a key; delete
- Verify: Confirmation dialog; key removed; undo brings it back
- Negative: Cancel maintains key
- Role: A,S

5) Add Section/Key (+)
- Steps: Use + to add a section; add keys; add row in foldperm/regperm/fileperm
- Verify: New items appear; values editable; minus removes row
- Role: A,S

6) Permissions Editors
- Steps: In foldperm/regperm/fileperm, add row; set Identity/Rights/Inheritance; save
- Verify: Validation for required fields; persisted on reload
- Role: A,S

7) Undo/Redo
- Steps: Make 3 edits across different keys; Ctrl+Z 3x; Ctrl+Y 3x
- Verify: Edits revert/restore in order across session
- Role: A,S

8) Save with Validation
- Steps: Leave a required value blank; click Save
- Verify: Inline validation highlights; save blocked; fix and save succeeds
- Negative: Attempt to save with invalid format (e.g., bad path token) → message shown
- Role: A,S

9) Variable Resolution
- Steps: Enable resolved view; use variables referencing Admin Settings
- Verify: Preview shows computed value; changing Admin path updates preview
- Role: A,S

10) Large INI Performance
- Steps: Load large INI (≥2,000 keys); scroll and edit
- Verify: UI remains responsive; save completes < 3s on test rig
- Role: A,S

11) Keyboard Shortcuts
- Steps: Ctrl+S (if supported), Ctrl+Z, Ctrl+Y, Ctrl+F search
- Verify: Actions performed; focus remains in editor
- Role: A,S

12) Error Handling & Recovery
- Steps: Simulate transient file lock; attempt save; retry
- Verify: Friendly error; no data loss; retry succeeds when lock released
- Role: A,S

