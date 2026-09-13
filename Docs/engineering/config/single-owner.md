# Single Authoritative Owner for Config

A setting must have one authoritative owner. Do NOT allow same operational setting independently editable from appsettings, environment, database, runtime state without explicit precedence rule. When multiple sources intentionally supported, precedence must be documented and deterministic.
