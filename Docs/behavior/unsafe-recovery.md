# Unsafe Workspace Recovery

Dangerous actions: git stash, reset, restore, checkout --, clean; broad rm/rm -rf. Especially dangerous with multiple ForgeGate sessions on same workspace.

Cross-session attribution and soft ownership must be considered before workspace recovery actions.
