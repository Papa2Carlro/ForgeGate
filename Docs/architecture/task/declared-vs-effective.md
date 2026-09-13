# Declared vs Effective Task Profile

Explicit metadata = declared profile. Runtime may maintain different effective profile.

Example: declared medium/normal → effective high/strict due to drift, failed decomposition, unexpected architecture, repeated failures, no-progress.

Reassessment adapts effective profile; does not repeatedly reclassify original prompt from scratch.
