# Changelog

## [2.1.0] - Unreleased

### Added
- Initialization graphs recorded in a development build are sent to the Editor over the player connection
  (`UnityEngine.Networking.PlayerConnection`); no XML is written on the device anymore.
- Initialization Graph window: **Source** selector (`Editor` / `Device`). `Device` reads snapshots from the
  connected player, **Request** asks players to resend the last recorded snapshot, and **Save XML...** stores
  the selected one on disk.
- Initialization Graph window: Profiler-style connection dropdown for `Device` - every discovered device with
  search, `Play Mode` / `Edit Mode` and `Direct Connection`. A snapshot is requested automatically when a
  player connects, received snapshots are labelled with the device name, and the snapshot list shows only the
  selected device unless **Show All Devices** is enabled.

### Changed
- `InitializationGraphRecording.Publish` no longer returns early when nothing is subscribed to
  `OnSnapshotRecorded`, so a snapshot still reaches the Editor.
- Documentation: the manual `persistentDataPath` + `adb pull` / Xcode container workflow is replaced by the
  player connection one.

## [2.0.0] - 2026-09-16

### Changed
- Replaced the batched execution with dependency-driven scheduling: every system starts as soon as its own
  dependencies are initialized, instead of waiting for the slowest system of its dependency level.
  The duration of a run is now the length of the critical path.
- `InitializationAsync` stops starting new systems on a failure and rethrows the original exception after the
  already running systems are awaited.
- Initialization bookkeeping (progress counters, critical systems, completion) is now fully synchronized,
  so systems completing on background threads cannot corrupt the counters.
- Initialization Graph window: edges implied by another dependency are hidden unless **All Edges** is enabled;
  selecting systems draws all of their direct edges and dims the systems they are not linked to.
- Initialization Graph window: timings from 500 ms on are shown in seconds (`10.5 s` instead of `10500 ms`).
- **Breaking:** snapshots are serialized as XML instead of JSON — `InitializationGraphSnapshot.ToJson` / `FromJson`
  are replaced by `ToXml` / `FromXml`, and the editor stores graphs in `Library/Pulse/Graphs` as `.xml`.
  Snapshots written by earlier versions cannot be restored, and `.json` files already in that folder are ignored.
- **Breaking:** `InitializationGraphSnapshot` and `InitializationSystemRecord` are no longer `[Serializable]`
  Unity types: their `[SerializeField]` backing fields are gone and the data is exposed as get-only properties.
  `JsonUtility` and `SerializedProperty` no longer work with them; use `ToXml` / `FromXml`.

### Added
- `IInitializationFramePacer` and `InitializationContextBuilder.SetFramePacer` — optional frame gate that
  postpones the next systems while the current frame is overloaded.
- `PlayerLoopFramePacer` — built-in pacer that hooks into the player loop without a `MonoBehaviour`.

### Removed
- `InitializationBatchRecord`, `InitializationGraphSnapshot.Batches` and `InitializationSystemRecord.BatchIndex`:
  batches no longer exist at runtime. The graph window derives dependency levels from `DependencyIndices`.
  Snapshots serialized by 1.x cannot be restored.

### Fixed
- Initialization Graph window: edges could be detached, deleted or reconnected with the mouse.

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
