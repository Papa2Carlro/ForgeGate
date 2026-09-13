# No Premature Enterprise Abstractions

Strong OOP/SOLID does not mean unnecessary frameworks. Do NOT add by default: MediatR; CQRS framework; generic Repository<T>; UnitOfWork wrapper over EF Core solely for pattern compliance; event bus with no real need; microservices; Redis; distributed messaging; reflection-heavy plugin system; generic policy/routing DSL. Introduce patterns only when actual ForgeGate requirement justifies them.
