# Provider Implementation Structure

Each provider has local provider-specific infrastructure module/folder (Infrastructure/Providers/OpenRouter/, XKiro/, Vireonix/). Includes transport/config; request normalization; response normalization; failure mapping; quirks. Shared OpenAI-compatible plumbing under Infrastructure/Providers/Common/ (HTTP plumbing, serialization, SSE parsing, reusable transport). Provider quirks remain local. No provider-name conditionals in routing/application.
