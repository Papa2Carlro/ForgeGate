# Routing Actions

Conceptual choices when preferred route unavailable: RETRY, WAIT, SWITCH_PROVIDER, DEGRADE_MODEL, ASK_USER, FAIL.

Sequence is policy-driven, not fixed globally. Same logical model via different provider = usually safe automatic. Significant degradation for complex task = ask user.
