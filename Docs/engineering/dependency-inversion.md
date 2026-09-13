# Dependency Inversion

Higher-level policy/application defines contracts it needs. Lower-level infrastructure implements them. Application/domain must not depend directly on provider HTTP clients, environment variables, concrete secret stores, EF Core DbContext, Npgsql, concrete provider adapters, concrete HTTP transport details. Concrete implementations wired at composition root.
