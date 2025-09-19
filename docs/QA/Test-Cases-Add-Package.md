# Test Cases — Add Package

1) Create New Package
- Steps: Fill App Name, Version, Vendor, etc.; generate INI
- Verify: INI created; appears in user DB; required fields validated

2) Import Upgrade INI (fix verification)
- Steps: Import INI containing upgrade section; import again another upgrade
- Verify: Sections created sequentially UPGRADE1, UPGRADE2, ... not duplicated

3) Toolkit Copy (if enabled)
- Steps: Use Copy Toolkit; select source; generate structure
- Verify: Folders/files created under BasePath as expected

4) Error Handling
- Steps: Leave mandatory field blank; try to proceed
- Verify: Blocking validation; helpful message

5) Persistence
- Steps: Save; navigate away; return
- Verify: Package persists; INI view matches saved content

