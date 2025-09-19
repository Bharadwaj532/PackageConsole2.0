# Test Cases — Package Dashboard

1) Default Load
- Steps: Open Dashboard; Submitted By = All (Master)
- Verify: Rows load from Master; counts logical

2) Switch Submitted By
- Steps: Change to a specific user database
- Verify: Grid updates; only that user’s packages appear

3) Refresh Master (Admin)
- Steps: As Admin, click Refresh Master
- Verify: Local build then copy to Central completes; Master view updates

4) CSV Export
- Steps: Export current view
- Verify: CSV downloads to chosen folder; header/rows match grid state and filters

5) Filters & Paging
- Steps: Apply text filters; clear
- Verify: Results refine/return; no stale filters

6) UNC/WAL Robustness
- Steps: Ensure central DBs on QA share; open All (Master)
- Verify: Data loads consistently (local temp copy approach works)

7) Error Handling
- Steps: Break central path; open Dashboard
- Verify: Clear error; instructions to fix Admin Settings

