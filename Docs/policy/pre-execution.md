# Pre-Execution Enforcement

Policy evaluates candidate tool action BEFORE committed to client for execution whenever protocol/tool flow permits.

Order: model generates candidate → ForgeGate receives/intercepts → policy evaluates → ALLOW or DENY/REPLAN/ASK_USER → only approved executable action reaches client. Post-execution logging is not sufficient.
