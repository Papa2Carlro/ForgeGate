# Cross-Session Protection

Detect actions that may destroy/interfere with another active track.

Conceptual signals: ForeignWorkspaceMutation, ConcurrentPathMutation, PotentialCrossTrackRevert, PotentialCrossTrackStash, OverlappingEditScope.

Example: Session A modifies foo.cs; Session B proposes reverting/stashing it. ForgeGate must attribute changes to Session A and prevent Session B from treating them as unexplained noise.
