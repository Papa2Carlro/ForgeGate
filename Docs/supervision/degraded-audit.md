# Auditability of Degraded Supervision

Whenever ForgeGate allows/rejects action because supervision subsystem degraded/unavailable, record as explicit structured fact. Conceptual examples: SemanticPolicyUnavailable; CompletionVerifierUnavailable; AllowedUnderFailOpen; DeniedUnderFailClosed; UserEscalationRequired. Event names/schema NOT finalized. Admin/audit must make degraded-supervision decisions visible.
