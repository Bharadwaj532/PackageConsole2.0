# Test Cases — Feedback

1) Submit Feedback
- Steps: Open Feedback; enter text; submit
- Verify: Entry stored and visible in list

2) View & Filter
- Steps: Open Feedback list; filter/search (if available)
- Verify: Items display correctly; time/user metadata sensible

3) Paths & Storage
- Steps: Locate stored feedback file(s) under BasePath
- Verify: Files created; readable; no PII beyond entered text

4) Error Handling
- Steps: Disconnect path; try to submit
- Verify: Graceful error shown; no crash; retry works after fix

