using System.Diagnostics;
using System.Management.Automation;
using System.Management.Automation.Language;

namespace Sashimi;

[Flags]
public enum OutputType
{
    Stdout = 1,
    Stderr = 2,
    All = Stdout | Stderr
}

[Cmdlet(VerbsLifecycle.Invoke, "RawCommand", DefaultParameterSetName = NormalParameterSet)]
[Alias("raw")]
public class InvokeRawCommandCommand : PSCmdlet
{
    private const string NormalParameterSet = "Normal";
    private const string ScriptBlockParameterSet = "ScriptBlock";

    private const int BufferLength = 4096;

    [Parameter(ParameterSetName = NormalParameterSet, Mandatory = true, Position = 0)]
    public string Command { get; set; } = null!;
    
    [Parameter(ParameterSetName = NormalParameterSet, ValueFromRemainingArguments = true, Position = 1)]
    public string[] Arguments { get; set; } = [];

    [Parameter(ParameterSetName = ScriptBlockParameterSet, Mandatory = true, Position = 0)]
    public ScriptBlock Script { get; set; } = null!;

    [Parameter(ValueFromPipeline = true)]
    public byte[]? InputBytes { get; set; }

    [Parameter()]
    public OutputType Output { get; set; } = OutputType.Stdout;

    private readonly List<byte[]> _stdinChunks = [];

    protected override void BeginProcessing()
        {
            base.BeginProcessing();
        }
    protected override void ProcessRecord()
    {
        if (InputBytes is not null)
        {
            _stdinChunks.Add(InputBytes);
        }
    }

    protected override void EndProcessing()
    {
        var psi = BuildProcessStartInfo();
        WriteVerbose($"[{psi.FileName}] Start with arguments: [{string.Join(", ", psi.ArgumentList)}]");

        using var proc = Process.Start(psi);

        if (proc is null)
            return;

        // stdin
        if (_stdinChunks.Count > 0)
        {
            WriteVerbose($"[{psi.FileName}] Write data into Stdin");
            var stdin = proc.StandardInput.BaseStream;
            foreach (var chunk in _stdinChunks)
                stdin.Write(chunk, 0, chunk.Length);
            stdin.Close();
        }

        var stdout = proc.StandardOutput.BaseStream;
        var stderr = proc.StandardError.BaseStream;

        if (Output.HasFlag(OutputType.Stdout))
        {
            WriteVerbose($"[{psi.FileName}] Output data in Stdout");
            WriteBytes(stdout);
        }

        if (Output.HasFlag(OutputType.Stderr))
        {
            WriteVerbose($"[{psi.FileName}] Output data in Stderr");
            WriteBytes(stderr);
        }

        proc.WaitForExit();
        WriteVerbose($"[{psi.FileName}] End [ExitCode = {proc.ExitCode}]");
        SessionState.PSVariable.Set("LASTEXITCODE", proc.ExitCode);
    }

    private void WriteBytes(Stream stream)
    {
        int read;
        Span<byte> buffer = stackalloc byte[BufferLength];
        while ((read = stream.Read(buffer)) > 0)
        {
            var chunk = buffer[..read].ToArray();
            WriteObject(chunk, false);
        }
    }

    private ProcessStartInfo BuildProcessStartInfo()
    {
        if (ParameterSetName is ScriptBlockParameterSet)
        {
            var ast = Script.Ast as ScriptBlockAst;
            var cmdAst = ast?.EndBlock.Statements.OfType<PipelineAst>()
                                                 .SelectMany(stmt => stmt.PipelineElements)
                                                 .FirstOrDefault(cmdAst => cmdAst is CommandAst) as CommandAst
                         ?? throw new InvalidOperationException("raw: no command specified");

            var arguments = cmdAst.CommandElements.Skip(1).Select(elem => elem.Extent.Text);
            return CreateProcessStartInfo(cmdAst.GetCommandName(), arguments);
        }
        else
        {
            ArgumentNullException.ThrowIfNullOrWhiteSpace(Command);
            return CreateProcessStartInfo(Command, Arguments);
        }

        ApplicationInfo GetCommand(string name)
            => InvokeCommand.GetCommand(name, CommandTypes.Application) as ApplicationInfo
                ?? throw new InvalidOperationException($"raw: command '{name}' not found");

        ProcessStartInfo CreateProcessStartInfo(string cmdName, IEnumerable<string> arguments)
            => new(GetCommand(cmdName).Path, arguments)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
            };
    }
}
