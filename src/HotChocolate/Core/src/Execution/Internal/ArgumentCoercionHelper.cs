using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using HotChocolate.Execution.Processing;
using HotChocolate.Execution.Processing.Tasks;
using HotChocolate.Resolvers;
using HotChocolate.Types;

namespace HotChocolate.Execution.Internal;

/// <summary>
/// This helper class implements the argument coercion algorithm.
/// </summary>
public static class ArgumentCoercionHelper
{
    /// <summary>
    /// Tries to coerce the arguments of a <see cref="ISelection"/>.
    /// </summary>
    /// <param name="arguments">
    /// The argument map from a <see cref="ISelection"/>.
    /// </param>
    /// <param name="resolverContext">
    /// The resolver context.
    /// </param>
    /// <param name="coercedArgs">
    /// The coerced arguments.
    /// </param>
    /// <returns>
    /// <c>true</c> if the arguments were successfully coerced; otherwise, <c>false</c>.
    /// </returns>
    public static bool TryCoerceArguments(
        this IArgumentMap arguments,
        IResolverContext resolverContext,
        [NotNullWhen(true)] out IReadOnlyDictionary<string, ArgumentValue>? coercedArgs)
    {
        if (arguments.IsFullyCoercedNoErrors)
        {
            coercedArgs = arguments;
            return true;
        }

        // if we have errors on the compiled execution plan we will report the errors and
        // signal that this resolver task has errors and shall end.
        if (arguments.HasErrors)
        {
            foreach (var argument in arguments.Values)
            {
                if (argument.HasError)
                {
                    resolverContext.ReportError(argument.Error!);
                }
            }

            coercedArgs = null;
            return false;
        }

        // if there are arguments that have variables and need variable replacement we will
        // rewrite the arguments that need variable replacement.
        Dictionary<string, ArgumentValue> args = new(StringComparer.Ordinal);
        CoerceArguments(arguments, resolverContext.Variables, args);
        coercedArgs = args;
        return true;
    }

    /// <summary>
    /// This internal helper allows the <see cref="ResolverTask"/> to coerce the argument
    /// values without allocating a dictionary for the argument values by letting the resolver task
    /// pass in a dictionary on which we coerce the argument values.
    /// </summary>
    internal static void CoerceArguments(
        this IArgumentMap arguments,
        IVariableValueCollection variableValues,
        Dictionary<string, ArgumentValue> coercedArgs)
    {
        // if there are arguments that have variables and need variable replacement we will
        // rewrite the arguments that need variable replacement.
        foreach (var argument in arguments.Values)
        {
            if (argument.IsFullyCoerced)
            {
                coercedArgs.Add(argument.Name, argument);
            }
            else
            {
                var literal = VariableRewriter.Rewrite(
                    argument.ValueLiteral!,
                    argument.Type,
                    argument.DefaultValue,
                    variableValues);

                coercedArgs.Add(argument.Name, new ArgumentValue(
                    argument,
                    literal.TryGetValueKind(out var kind) ? kind : ValueKind.Unknown,
                    argument.IsFullyCoerced,
                    argument.IsDefaultValue,
                    null,
                    literal));
            }
        }
    }
}
