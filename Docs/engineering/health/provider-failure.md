# Provider Failure Does Not Automatically Mean Not Ready

Single provider/model route unavailable must not make entire ForgeGate process unready. Example: xKiro unavailable + OpenRouter available → ForgeGate remains ready. Even temporary lack of any currently usable model route is operational routing state, not process/app readiness failure. API/Admin surface may still function correctly and return explicit no-route result.
