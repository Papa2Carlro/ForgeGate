# Liveness and Readiness

Separate semantics. /liveness → process/application alive. /readiness → ForgeGate initialized sufficiently to service requests as system. Exact endpoint paths follow ASP.NET health-check conventions during implementation, but semantic separation mandatory.
