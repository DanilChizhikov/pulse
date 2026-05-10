# Changelog

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
