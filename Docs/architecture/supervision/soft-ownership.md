# Soft Ownership + Escalation

No hard file locking. Soft activity attribution / intent is maintained.

Path attribution includes: recently touched by session, active mutation intent, confidence/recency.
Used as policy/supervision signal, not exclusive ownership.

Examples: reading file used by another session → ALLOW; overlapping mutation → REPLAN/ASK_USER; reverting/stashing/deleting another session's work → DENY.
