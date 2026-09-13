# Database Transaction Ownership

Transaction boundaries determined by use-case consistency requirement. Not automatically: each repository call = own transaction; each HTTP request = one giant DB transaction; every use case = generic UnitOfWork.
