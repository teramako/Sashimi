# Changelog

## [Unreleased]

### Changed
- Changed `RawProcessRunner` from `public` to `internal` and moved it into the `Sashimi.Internal` namespace.
  - This type is an implementation detail and was never intended to be part of the public API surface.
  - If external code directly referenced `RawProcessRunner`, this constitutes a breaking change. Please migrate to the supported cmdlet-based APIs as needed.

### Internal
- Consolidated internal implementation types under the `Sashimi.Internal` namespace to clearly separate public API from internal architecture.
- Moved internal types such as `RawExecutionEngine`, `ExecutionEngine`, `NativeCommandCompleter`, and `EncodingCompleter` into the `Internal/` directory.
- Extracted `RawOutputItem` and related record types from `InvokeRawCommandCommand` and centralized them under `Sashimi.Internal`.

- Refactored `RawExecutionEngine` string decoding:
  - Added `PipeStringDecoder` to unify stdout/stderr decoding.
  - Replaced pipe fields with decoder fields.
  - `RawOutputRecord` now carries `OutputType` for unified output routing.
  - Simplified `RawExecutionEngine` by delegating all string decoding to `PipeStringDecoder`.

- Replaced `RawOutputItem` with `RawOutputRecord` to clarify the internal output model.
- Added `ToString()` implementations for `ChunkOutput` (hex dump) and `StringOutput`.
- Updated internal components (`PipeStringDecoder`, `RawExecutionEngine`) to use the new record types.
- Improved stderr output formatting by using `RawOutputRecord.ToString()` when generating `InformationRecord`.

## 1.2.0 - 2026-07-04

### Added
- Added `NativeCommandCompleter` for `-Command` argument completion in `Invoke-RawCommand`.
  - Provides completion for native executables. (excludes cmdlets, functions and aliases).
- Added `EncodingCompleter` for `-Encoding` argument completion in `Invoke-RawCommand`, `ConvertTo-RawString` and `ConvertFrom-RawString`.
  - Provides completion for all installed encodings.
  - Includes alias completion and descriptive tooltips (code page, display name).
- Added `-Encoding` parameter to control decoding of stdout/stderr (default: UTF-8).
- Added string-based stderr output: stderr byte chunks are now decoded using the specified encoding and emitted as `InformationRecord` with appropriate colorization and tags.

### Improved
- Refined process completion model to ensure correct ordering of *process exit → output EOF → safe stream close*.  
  This eliminates race conditions when using PipeStream and guarantees stable shutdown behavior for `Invoke-RawCommand`.

### Fixed
- Fixed premature-close issues where stdout/stderr streams could be closed before read loops finished, causing occasional `ObjectDisposedException` or incomplete output consumption.
- `Invoke-RawCommand` now waits on unified completion (`WaitForCompleteAsync`) ensuring that `stringReaderTask` always observes EOF and terminates cleanly.

### Internal
- Added `WaitForCompleteAsync` to `RawProcessRunner` to unify process-exit and output-completion waiting.
- Strengthened `WaitOutputAsync` to close stdout/stderr only after read loops fully complete.
- Updated `InvokeRawCommandCommand` to rely on the new completion model instead of manually coordinating multiple tasks.

## 1.1.1 - 2026-06-28

### Fixed
- Fixed a pipeline binding issue in `Invoke-RawCommand` where parameter-set
  resolution could fail when receiving pipeline input, causing Script/Command
  detection to break. Simplified binding logic and removed parameter-set-specific
  handling for `-AsString`.

- Avoided a rare race condition on Linux/WSL where reading `Process.StartTime`
  could throw a `Win32Exception` if `/proc/<pid>/stat` was not yet available.
  A safe timestamp is now used for verbose logging.

## 1.1.0

### Improved
- Enhanced `-Verbose` output to include cmdlet execution time, process StartTime/ExitTime, and Duration for better debugging visibility.
- Refactored `RawProcessRunner` for full async/cancellation safety.
  - Replaced `StartAsync` with synchronous `Start` and introduced `WaitOutputAsync`.
  - Propagated `PipelineStopToken` through all process I/O paths (stdin, stdout, stderr, exit wait).
  - Registered `Kill()` on cancellation to ensure child processes terminate reliably.
  - Strengthened `DisposeAsync` to dispose the cancellation registration, kill the process, and await output tasks.
  - Added `CancellationToken` support to `WriteStdinAsync`.
  - Improved read-loop robustness by catching `OperationCanceledException` quietly.

### Notes
- These changes affect only internal implementation details and do not alter the public cmdlet API.
- Cancellation behavior is now more deterministic and consistent across all process operations.
