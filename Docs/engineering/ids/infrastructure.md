# Strongly-Typed ID Infrastructure

Serialization/EF conversion for strongly-typed IDs should be configured centrally/reusably where practical. Do NOT duplicate conversion boilerplate unnecessarily across every entity. Do NOT introduce large reflection/magic framework solely to support ID wrappers.
