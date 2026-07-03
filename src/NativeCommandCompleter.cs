using System.Collections;
using System.Management.Automation;
using System.Management.Automation.Language;

namespace Sashimi;

public class NativeCommandCompleter : IArgumentCompleter
{
    public IEnumerable<CompletionResult> CompleteArgument(string commandName,
                                                          string parameterName,
                                                          string wordToComplete,
                                                          CommandAst commandAst,
                                                          IDictionary fakeBoundParameters)
    {
        return CompletionCompleters.CompleteCommand(wordToComplete, string.Empty, CommandTypes.Application);
    }
}
