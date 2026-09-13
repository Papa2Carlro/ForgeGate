# Background Worker Responsibility

Each worker has one coherent responsibility. No ForgeGateBackgroundWorker accumulating health probes, cleanup, model discovery, projection refresh, recovery, miscellaneous scheduling. Workers reuse application contracts/state owners rather than bypassing architecture and reaching directly into unrelated infrastructure internals.
