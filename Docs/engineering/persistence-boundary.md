# Persistence Boundary

Application must not depend directly on EF Core DbContext. No generic Repository<T>. No UnitOfWork wrapper solely for pattern compliance. Focused persistence ports per feature/use case. Examples: IActivityStore.AppendAsync(...), IActivityStore.QueryRecentAsync(...); ISessionStore.GetAsync(...), ISessionStore.SaveAsync(...). Not IRepository<T>.GetAll/Insert/Update/Delete. No IQueryable or EF-specific concepts in Application contracts.
