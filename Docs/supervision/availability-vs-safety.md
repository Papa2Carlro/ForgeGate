# Availability vs Safety Principle

ForgeGate failure must neither: (1) unnecessarily stop all useful agent work; nor (2) silently remove protections around risky mutations. Therefore: low-risk/read-only → availability-biased; high-risk mutation → safety-biased; completion certification → evidence-biased.
