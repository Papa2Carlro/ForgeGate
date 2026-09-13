# Action Uncertainty

Connection failure does not prove action did not execute.

Conceptual lifecycle: proposed; committed to client; execution observed; result observed; unknown.

Schema NOT decided. For UNKNOWN state, reconcile actual state before retrying non-idempotent actions (filesystem mutation, git, migrations, package install, deployment).
