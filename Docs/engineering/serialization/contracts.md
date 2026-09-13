# Serialization Contract Ownership

Serialized contracts belong to their boundary. OpenAI transport DTOs → Api/Protocol boundary. Provider payloads → Infrastructure/Providers/<Provider>. Admin HTTP DTOs → Admin/API boundary. Internal application/domain models → not automatically persistence DTOs. Do NOT reuse one DTO everywhere merely to reduce mapping code.
