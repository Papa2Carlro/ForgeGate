# Separation of Concerns

Boundaries preserved:
- Provider routing: health, quotas, capacity, latency, availability.
- Execution supervision: repeated actions, no-progress, long-running, scope drift.
- Policy enforcement: prohibited/risky actions, workspace mutation, user restrictions.
- Semantic reasoner: ambiguous intent/proportionality.
- Planning supervisor: future concern for poor decomposition.

May exchange signals/context; must not collapse into one god service.
