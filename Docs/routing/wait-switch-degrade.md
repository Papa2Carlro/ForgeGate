# Wait / Switch / Degrade Policy

Profile-driven wait budget + degradation budget + task-aware quality floor. Not always fail over immediately; not always wait indefinitely.

Conceptual flow: preferred unavailable → recover within wait budget? YES → WAIT/RETRY. NO → same logical model through another provider? YES → SWITCH_PROVIDER. NO → acceptable lower-tier model? YES → DEGRADE or ASK_USER. NO → WAIT/FAIL per policy.

Profiles: fast (small wait, aggressive degrade); balanced (moderate); smart (larger wait, conservative); coding (complexity/quality floor strongly affects degradation).
