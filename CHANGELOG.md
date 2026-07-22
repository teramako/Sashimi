# Changelog

## [Unreleased]

### Changed

- `LASTEXITCODE` is no longer set when the exit code of an external command cannot be retrieved
  (e.g., command not found, permission error, internal error).
  This behavior now matches PowerShell’s own behavior.

- Replaced `InvalidOperationException` with `CommandNotFoundException` when a command cannot be resolved.
  This aligns the error behavior with PowerShell’s standard command resolution errors.

- Non-zero exit code failures now use a dedicated ErrorId (`ExternalCommandNonZeroExit`)
  and `InvalidResult` category, clearly separated from internal processing errors.

### Added

- Introduced `Test-RawCommand` (alias: `raw?`)
  - Executes an external command and returns `true` when the exit code is `0`, otherwise `false`.
  - Emits stdout and stderr as `Information` records tagged with their originating stream.

- `ExternalCommandNonZeroExitException` is now thrown when `Invoke-RawCommand`
  is executed with `-ThrowOnError` and the external command returns a non-zero exit code.

### Fixed

- Fixed an issue where external commands that exit early (e.g., due to invalid arguments)
  could cause `Invoke-RawCommand` to hang or throw a large `AggregateException`.  
  Stdin writes now detect early process termination, ignore broken pipe errors,
  and correctly close the input stream to allow the pipeline to complete.

### Internal
- `RawExecutionEngine` is now extensible and no longer tied to `InvokeRawCommandCommand`.
  This enables custom execution engines (e.g., for testing or specialized behaviors)
  to derive from RawExecutionEngine.

## 2.0.0 - 2026-07-18

### Changed

- Added support for string pipeline input in `Invoke-RawCommand`.
  String input is now encoded using the encoding specified by `-Encoding` (default: UTF-8).
  This enables correct stdin handling for tools expecting non-UTF8 encodings such as Shift_JIS or EUC-JP.

### Added

- Introduced **ScriptBlock mode** for `Invoke-RawCommand`.
  - Added `ScriptBlockExecutionEngine`, enabling raw execution inside `raw { ... }` blocks.
  - External commands inside a ScriptBlock are now detected and rewritten to use `Invoke-RawCommand` automatically.
  - External commands inside a ScriptBlock are now grouped into chains, enabling precise control over `-AsString` propagation.
  - This allows multi-statement ScriptBlocks to be executed without the previous single-command limitation.
  - Apply `-AsString` parameter only to the last external command in each pipeline segment.

### Fixed

- External commands now correctly use the current file system directory when executed in raw mode,
  fixing issues where relative paths failed after changing location.

## 1.3.0 - 2026-07-15

### Changed
- Stderr is now emitted as `ErrorRecord` instead of `InformationRecord`.
  - Enables correct PowerShell redirection behavior (`2>`, `2>&1`).
  - Uses `ErrorCategory.FromStdErr` for native stderr classification.

- Changed `RawProcessRunner` from `public` to `internal` and moved it into the `Sashimi.Internal` namespace.
  - This type is an implementation detail and was never intended to be part of the public API surface.
  - If external code directly referenced `RawProcessRunner`, this constitutes a breaking change. Please migrate to the supported cmdlet-based APIs as needed.

### Internal

#### Introduced `Sashimi.Internal` namespace
- Consolidated internal implementation types under the `Sashimi.Internal` namespace to clearly separate public API from internal architecture.

#### Unified output model
- Replaced `RawOutputItem` with `RawOutputRecord` to clarify the internal output representation.
- Added `ChunkOutput` (hex dump) and `StringOutput` with meaningful `ToString()` implementations.
- Updated internal components (`PipeStringDecoder`, `RawExecutionEngine`) to use the new record-based model.

#### Centralized string decoding
- Introduced `PipeStringDecoder` to unify stdout/stderr string decoding.
- Removed per-stream pipe fields and delegated all decoding logic to `PipeStringDecoder`.
- Improved stderr formatting by using `RawOutputRecord.ToString()` when generating `InformationRecord`.

#### Redirection model redesign
- Renamed `OutputType` → `OutputFrom` to clarify “source stream” semantics.
- Introduced `RedirectTo` enum (Null / Output / Error) aligned with PowerShell’s `RedirectionStream`.
- Added `Redirection` record struct to unify initial output selection and final routing targets.
- Updated `RawExecutionEngine`, `PipeStringDecoder`, and `RawOutputRecord` to use the new routing model.

#### PowerShell redirection parsing
- Implemented parsing of PowerShell redirection syntax (`2>&1`, `>&2`, `*>$null`, etc.) using `RedirectionAst`.
- Extended `RedirectTo` to include all PowerShell streams (Warning, Verbose, Debug, Information).
- Added `Redirection.GetRedirectionFromStatement()` to merge `OutputFrom` with parsed redirection rules.
- Updated `RawExecutionEngine` to route output based on final `RedirectTo` targets, including correct byte[]/string behavior for merged stderr→stdout.

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
