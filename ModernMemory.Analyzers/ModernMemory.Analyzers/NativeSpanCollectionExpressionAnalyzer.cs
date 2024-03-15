using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ModernMemory.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class NativeSpanCollectionExpressionAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "NativeSpanCollectionExpression";

        // You can change these strings in the Resources.resx file. If you do not want your analyzer to be localize-able, you can use regular strings for Title and MessageFormat.
        // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/Localizing%20Analyzers.md for more on localization
        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.AnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.AnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.AnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = "Performance";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public const string NativeSpanName = "ModernMemory.NativeSpan`1";

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(compilationContext =>
            {
                var analyzer = new Analyzer();
                // Register an intermediate non-end action that accesses and modifies the state.
                compilationContext.RegisterSyntaxNodeAction(analyzer.AnalyzeSyntaxNode, SyntaxKind.VariableDeclaration);

                // Register an end action to report diagnostics based on the final state.
                compilationContext.RegisterCompilationEndAction(analyzer.CompilationEndAction);
                
            });
        }

        private sealed class Analyzer
        {
            public void AnalyzeSyntaxNode(SyntaxNodeAnalysisContext context)
            {
                var node = context.Node;
                switch (node)
                {
                    case VariableDeclarationSyntax variableDeclaration:
                        var model = context.SemanticModel;
                        var nativeSpanType = model.Compilation.GetTypeByMetadataName(NativeSpanName);
                        var vtype = model.GetTypeInfo(variableDeclaration.Type).Type?.OriginalDefinition;
                        if (vtype is { } && vtype.Equals(nativeSpanType, SymbolEqualityComparer.Default))
                        {
                            foreach (var item in variableDeclaration.ChildNodes().OfType<VariableDeclaratorSyntax>().SelectMany(a => a.DescendantNodes().OfType<CollectionExpressionSyntax>()))
                            {
                                context.ReportDiagnostic(Diagnostic.Create(Rule, item.GetLocation()));
                            }
                        }

                    break;
                }
            }

            public void CompilationEndAction(CompilationAnalysisContext context)
            {

            }
        }
    }
}
