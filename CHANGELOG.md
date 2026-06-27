# Changelog

## [Unreleased]

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
