# Changelog

## ## 1.1.1 - 2026-06-28

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
