# Integration with Internal Correction Loop

Denied/rejected candidate action fed back internally to same model with bounded corrective context so model can replan. Conceptual flow: model → candidate → policy denial → ForgeGate corrective feedback → same model replans → policy evaluates again. Retry/replan loops bounded. Internal correction turns not exposed to client as ordinary assistant output unless protocol/failure handling requires.
