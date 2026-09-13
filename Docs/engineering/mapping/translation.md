# Mapping / Translation

Explicit mapping/translation at architectural boundaries. Conceptual pipeline: OpenAI DTO → explicit protocol mapper → canonical ForgeGate model → provider request normalizer → provider transport → provider response normalizer → canonical ForgeGate response → explicit OpenAI response mapper.
