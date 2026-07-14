using System.Management.Automation.Language;

namespace Sashimi;

internal enum RedirectTo
{
    Null = 0,
    Output = RedirectionStream.Output,
    Error = RedirectionStream.Error,
    Warning = RedirectionStream.Warning,
    Verbose = RedirectionStream.Verbose,
    Debug = RedirectionStream.Debug,
    Information = RedirectionStream.Information
}

