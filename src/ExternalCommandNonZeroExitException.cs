using System.Management.Automation;

namespace Sashimi;

public sealed class ExternalCommandNonZeroExitException : ApplicationFailedException
{
    public int ExitCode { get; }

    public ExternalCommandNonZeroExitException(string message, int exitCode) : base(message)
    {
        ExitCode = exitCode;
    }
}
