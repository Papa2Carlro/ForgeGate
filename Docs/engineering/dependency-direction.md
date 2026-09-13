# Dependency Direction

Infrastructure details must not become dependencies of core business logic. Routing logic must not depend directly on EF Core DbContext, Npgsql, HttpClient details, environment variables, ASP.NET HttpContext, concrete provider adapters. Infrastructure implements contracts consumed by higher-level logic. Transport/API DTOs must not leak deeply into application/domain logic.
