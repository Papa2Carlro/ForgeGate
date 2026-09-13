# Testability

Important behavior testable without real external providers. Important components support deterministic tests with substitute implementations at architectural boundaries. Router with fake route state; CapacityCoordinator without real provider; Policy without real VS Code client; failure normalization with fixtures; provider adapters separately from routing. Design around meaningful contracts; testability follows from boundaries. Not design around mocks.
