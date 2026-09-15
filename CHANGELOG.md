# Changelog

## [1.3.0] - Unreleased

## [1.2.0] - 2026-09-15

### Added
- Opt-in initialization graph recording via `InitializationGraphRecording` (`IsEnabled`, `OnSnapshotRecorded`).
  Captures batches, dependencies, start order, start offset, duration and status of every system.
- `InitializationGraphSnapshot` with `ToJson` / `FromJson` for dumping graphs from player builds.
- Editor toggle `Tools/DTech/Pulse/Record Initialization Graph`; snapshots are saved to `Library/Pulse/Graphs`.
- `Window/DTech/Pulse/Initialization Graph` GraphView window for browsing recorded snapshots.

## [1.1.0] - 2026-05-10

### Added
- IL2CPP reflection preservation support through `link.xml`.
- Expanded initialization tests and benchmarks for edge cases and performance coverage.

### Changed
- Made dependency discovery deterministic.
- Made constructor dependency discovery stricter when systems have multiple constructors.
- Clarified README runtime compatibility and managed stripping guidance.

### Fixed
- Duplicate runtime system registration validation.
- Initialization graph locking and node-handle mutation after `Build()`.
- Ambiguous assignable dependency resolution while preserving exact dependency matches.
- Critical-system callback cleanup and thread-safe critical initialization event invocation.

## [1.0.0] - 2025-11-29

Initial release.
