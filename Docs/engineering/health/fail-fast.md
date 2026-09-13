# Fail-Fast Startup

Mandatory invalid bootstrap configuration must fail startup clearly. Examples: malformed required database config; impossible mandatory internal config; invalid required composition; required schema incompatible with running application where migration policy requires failure. Do NOT silently start partially invalid instance when defect prevents correct operation.
