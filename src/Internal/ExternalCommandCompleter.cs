using System.Collections;
using System.Management.Automation;
using System.Management.Automation.Language;

namespace Sashimi.Internal;

/// <summary>
/// Provides argument completion for native executables
/// (excluding cmdlets, functions, and aliases).
/// </summary>
internal class ExternalCommandCompleter : IArgumentCompleter
{
    public IEnumerable<CompletionResult> CompleteArgument(string commandName,
                                                          string parameterName,
                                                          string wordToComplete,
                                                          CommandAst commandAst,
                                                          IDictionary fakeBoundParameters)
    {
        wordToComplete ??= string.Empty;
        return CompletionCompleters.CompleteCommand(wordToComplete, string.Empty, CommandTypes.Application);
    }
}
