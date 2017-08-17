using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PreserveFormattableStringForObjParams
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class PreserveFormattableStringForObjParamsAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "PreserveFormattableStringForObjParams";

        public static readonly string Title = "An Interpolated string ($\"...\") is cast to normal string " +
            "during argument passing. This will lose information about raw data value. " +
            "Raw values should be preserved by passing it as FormattableString.";

        public static readonly string MessageFormat = 
            "Raw data values from interpolated string are lost due to cast to an object.";

        public static readonly string Description = "An interpolated string is being passed to an 'object' parameter, " +
            "this will cast it to a normal string and lose information about raw data values." +
            "Raw values should be preserved by passing it as FormattableString.";

        private const string Category = "FormattableString";

        private static DiagnosticDescriptor Rule = 
            new DiagnosticDescriptor(DiagnosticId, 
                Title, 
                MessageFormat, 
                Category, 
                DiagnosticSeverity.Error, 
                isEnabledByDefault: true, 
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.Method);
        }

        private static void AnalyzeSymbol(SymbolAnalysisContext context)
        {
            var methodSymbol = (IMethodSymbol)context.Symbol;

            var syntaxReference = context.Symbol.DeclaringSyntaxReferences.Single();

            var declaringNode = syntaxReference
                                    .SyntaxTree
                                    .GetRoot()
                                    .FindNode(syntaxReference.Span) as MethodDeclarationSyntax;

            var semanticModel = context.Compilation.GetSemanticModel(syntaxReference.SyntaxTree);

            var inlineInterpolatedStringParameters =
                    declaringNode.DescendantNodes()
                                 .OfType<InvocationExpressionSyntax>()
                                 .Select(node => new SuspectArguments(                                                    
                                                    GetInterpolatedStringArgumentPositions(node).ToArray(),
                                                    node))
                                 .Where(suspect => suspect.AgrPositions.Any())
                                 .ToArray();

            if(inlineInterpolatedStringParameters.Any() == false)
            {
                return;
            }

            var verifiedObjectConversions =
                inlineInterpolatedStringParameters
                    .Select(x => FilterForInterpolatedStringsConvertedToObject(x, semanticModel))
                    .Where(suspect => suspect.AgrPositions.Any())
                    .ToArray();

            foreach(var suspect in verifiedObjectConversions)
            foreach(var argPosition in suspect.AgrPositions)
            {
                var arg = suspect.Node.ArgumentList.Arguments[argPosition];
                // For all such symbols, produce a diagnostic.

                var diagnostic = Diagnostic.Create(
                                                Rule,
                                                arg.GetLocation());

                context.ReportDiagnostic(diagnostic);
            }
        }

        private class SuspectArguments
        {
            public int[] AgrPositions { get; private set; }
            public InvocationExpressionSyntax Node { get; private set; }

            public SuspectArguments(int[] argPositions, InvocationExpressionSyntax node)
            {
                AgrPositions = argPositions;
                Node = node;
            }
        }

        private static SuspectArguments FilterForInterpolatedStringsConvertedToObject(
            SuspectArguments susect,
            SemanticModel semanticModel)
        {
            var methodInfo = semanticModel.GetSymbolInfo(susect.Node).Symbol as IMethodSymbol;
            
            if(methodInfo == null)
            {
                return new SuspectArguments(new int[0], susect.Node);
            }

            var parameters = methodInfo.Parameters.ToArray();
            var arguments = susect.Node.ArgumentList.Arguments.ToArray();
            var functionIsVariadic = parameters.LastOrDefault()?.IsParams ?? false;
            var confirmedArgConversionPositions = new List<int>();
                
            for(int count = 0; count < arguments.Length; count++)
            {
                var argumentIsPassedToLastParameter = count >= (parameters.Length - 1);

                var argument = arguments[count];
                IParameterSymbol param;
                if(TryGetNameOfAgrumentPassedByName(argument, out string name))
                {
                    param = parameters.Where(x => x.Name == name).Single();
                }
                else if(functionIsVariadic && argumentIsPassedToLastParameter)
                {
                    param = parameters.Last();
                }
                else
                {
                    param = parameters.ElementAt(count);
                }

                if(ArgumentWillBeConvertedToString(param, argument))
                {
                    confirmedArgConversionPositions.Add(count);
                }
            }

            return new SuspectArguments(
                confirmedArgConversionPositions.ToArray(), 
                susect.Node);
        }

        private static bool ArgumentWillBeConvertedToString(IParameterSymbol param, ArgumentSyntax arg)
        {
            // naive check to for casts and 'as' clauses
            var formattableStringIsMentioned = arg
                        .DescendantTokens()
                        .Where(x => x.Kind() == SyntaxKind.IdentifierToken)
                        .Any(x => x.Text.ToString().Trim() == "FormattableString");

            if (formattableStringIsMentioned)
            {
                return false;
            }

            bool ITypeSymbolIsObject(ITypeSymbol typeSymbol)
            {
                return typeSymbol.ContainingNamespace.Name == "System"
                && typeSymbol.Name == "Object";
            }

            if(param.IsParams 
                && ITypeSymbolIsObject(((IArrayTypeSymbol)param.Type).ElementType))
            {
                return true;
            }

            if (ITypeSymbolIsObject(param.Type))
            {
                return true;
            }

            return false;
        }

        private static bool TryGetNameOfAgrumentPassedByName(ArgumentSyntax arg, out string name)
        {
            var nameClause = arg.NameColon?.GetText().ToString().Replace(":", "").Trim();
            if (string.IsNullOrWhiteSpace(nameClause))
            {
                name = null;
                return false;
            }

            name = nameClause;
            return true;
        }

        private static IEnumerable<int> GetInterpolatedStringArgumentPositions(InvocationExpressionSyntax invocation)
        {
            var arguments = invocation.ArgumentList.Arguments.ToArray();

            for (int count = 0; count < arguments.Length; count++)
            {
                var argument = arguments[count];
                var containsInterpolatedString = 
                                    argument.ChildNodes()
                                            .Any(node => node is InterpolatedStringExpressionSyntax);

                if (containsInterpolatedString)
                {
                    yield return count;
                }
            }
        }
    }
}
