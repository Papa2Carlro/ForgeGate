# Admin Architectural Direction

Even with smaller first UI, avoid backend APIs that prevent future expansion. Architecture must permit movement from Operational Admin → Full Control Plane without rewriting core models. Does NOT justify speculative APIs or generic abstractions now. Apply: clean boundaries for future expansion; implement only demonstrated MVP requirements.
