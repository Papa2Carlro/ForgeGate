# Recovery Strategy Status

DECIDED:
- transparent request-level failover before commit.
- no model-stream splicing after commit.
- structured task-level recovery after commit.
- structured handoff rather than giant master prompt.
- checkpoint + facts + workspace state + semantic summary.
- explicit distinction between facts and model interpretation.
- unknown side-effect state must be reconciled before retry.
- recovery route must respect task/model compatibility.

NOT YET DECIDED:
- exact streaming commit threshold; exact checkpoint trigger policy; exact checkpoint persistence format; exact handoff DTO; exact action lifecycle schema; exact recovery compatibility score; whether models can explicitly request checkpoints; exact retention policy for checkpoints/events.
