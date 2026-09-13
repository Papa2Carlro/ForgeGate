# SOLID

SRP: one coherent reason to change (routing, failure normalization, provider transport, capability evaluation, capacity, policy, completion verification separate).
OCP: extensible through composition/implementations, not central switch edits.
LSP: implementations preserve contracts.
ISP: small focused contracts, not giant ILLMProvider with Chat/Stream/ListModels/GetQuota/Health/Vision/Tools.
DIP: higher-level logic depends on abstractions, not concrete provider/DB/secret implementations.
