﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CodeStyle;
using Microsoft.CodeAnalysis.CSharp.CodeStyle;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.CSharp.SimplifyPropertyPattern
{
    /// <summary>
    /// Looks for code of the form:
    /// 
    ///     <c>x is { a: { b: ... } }</c>
    ///     
    /// and converts it to:
    /// 
    ///     <c>x is { a.b: ... }</c>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    internal class CSharpSimplifyPropertyPatternDiagnosticAnalyzer : AbstractBuiltInCodeStyleDiagnosticAnalyzer
    {
        public CSharpSimplifyPropertyPatternDiagnosticAnalyzer()
            : base(IDEDiagnosticIds.SimplifyPropertyPatternDiagnosticId,
                   EnforceOnBuildValues.SimplifyPropertyPattern,
                   CSharpCodeStyleOptions.PreferExtendedPropertyPattern,
                   LanguageNames.CSharp,
                   new LocalizableResourceString(nameof(CSharpAnalyzersResources.Property_pattern_can_be_simplified), CSharpAnalyzersResources.ResourceManager, typeof(CSharpAnalyzersResources)),
                   new LocalizableResourceString(nameof(CSharpAnalyzersResources.Simplify_property_pattern), CSharpAnalyzersResources.ResourceManager, typeof(CSharpAnalyzersResources)))
        {
        }

        public override DiagnosticAnalyzerCategory GetAnalyzerCategory()
            => DiagnosticAnalyzerCategory.SemanticSpanAnalysis;

        protected override void InitializeWorker(AnalysisContext context)
        {
            context.RegisterCompilationStartAction(compilationContext =>
            {
                // Dotted property patterns are only available in C# 10.0 and above.  Don't offer this refactoring
                // in projects targeting a lesser version.

                var compilation = compilationContext.Compilation;
                if (((CSharpCompilation)compilation).LanguageVersion < LanguageVersion.CSharp10)
                    return;

                context.RegisterSyntaxNodeAction(AnalyzeSubpattern, SyntaxKind.Subpattern);
            });
        }

        private void AnalyzeSubpattern(SyntaxNodeAnalysisContext syntaxContext)
        {
            var options = syntaxContext.Options;
            var syntaxTree = syntaxContext.Node.SyntaxTree;
            var cancellationToken = syntaxContext.CancellationToken;

            // Bail immediately if the user has disabled this feature.
            var styleOption = options.GetOption(CSharpCodeStyleOptions.PreferExtendedPropertyPattern, syntaxTree, cancellationToken);
            if (!styleOption.Value)
                return;

            var severity = styleOption.Notification.Severity;
            var subpattern = (SubpatternSyntax)syntaxContext.Node;
            if (!SimplifyPropertyPatternHelpers.IsSimplifiable(subpattern, out _, out var expressionColon))
                return;

            // If the diagnostic is not hidden, then just place the user visible part
            // on the local being initialized with the lambda.
            syntaxContext.ReportDiagnostic(DiagnosticHelper.Create(
                Descriptor,
                expressionColon.GetLocation(),
                severity,
                ImmutableArray.Create(subpattern.GetLocation()),
                properties: null));
        }
    }
}
