# ForgeGate Code Architecture

Mandatory engineering rules for all implementation agents.

OOP = REQUIRED. SOLID = REQUIRED. Dependency injection (ASP.NET Core) = REQUIRED. Explicit module boundaries = REQUIRED. Replaceability = REQUIRED. Thread safety for shared state = REQUIRED. OpenAPI/Swagger for HTTP API = REQUIRED. Focused testability = REQUIRED.

INTERFACE_FOR_EVERY_CLASS = NOT_REQUIRED. EVERY_SERVICE_SINGLETON = FORBIDDEN_AS_A_RULE. MICROSERVICES = NOT_REQUIRED. CQRS/MEDIATR = NOT_REQUIRED. GENERIC_REPOSITORY = NOT_REQUIRED. PREMATURE_ABSTRACTION = DISCOURAGED.
