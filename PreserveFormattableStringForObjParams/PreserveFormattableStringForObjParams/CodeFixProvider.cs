using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;
using SF = Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace PreserveFormattableStringForObjParams
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PreserveFormattableStringForObjParamsCodeFixProvider)), Shared]
    public class PreserveFormattableStringForObjParamsCodeFixProvider : CodeFixProvider
    {
        private const string title = "Add explicit cast to preserver FormattableString";

        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(PreserveFormattableStringForObjParamsAnalyzer.DiagnosticId); }
        }

        public sealed override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document
                                  .GetSyntaxRootAsync(context.CancellationToken)
                                  .ConfigureAwait(false);

            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            var argument = root.FindToken(diagnosticSpan.Start)
                                    .Parent.AncestorsAndSelf()
                                    .OfType<ArgumentSyntax>().First();

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: title,
                    createChangedSolution: c => AddExplicitCastToFormattableString(context.Document, argument, c),
                    equivalenceKey: title),
                diagnostic);
        }

        private async Task<Solution> AddExplicitCastToFormattableString(
                                                            Document document, 
                                                            ArgumentSyntax argument, 
                                                            CancellationToken cancellationToken)
        {
            var interpolatedStrings = argument
                                        .DescendantNodes()
                                        .OfType<InterpolatedStringExpressionSyntax>()
                                        .ToArray();

            var currentArgument = argument;
            var FormattedStringTypeIdentifier = SF.IdentifierName("FormattableString");

            foreach (var currentStirng in interpolatedStrings)
            {
                var syntaxtWithCast = SF.CastExpression(FormattedStringTypeIdentifier, currentStirng);
                currentArgument = currentArgument.ReplaceNode(currentStirng, syntaxtWithCast);
            }

            var root = await document.GetSyntaxRootAsync();

            var newRoot = root.ReplaceNode(argument, currentArgument);

            var newSolution = document.Project.Solution
                                              .RemoveDocument(document.Id)
                                              .AddDocument(document.Id, document.Name, newRoot);

            return newSolution;

        }
    }
}