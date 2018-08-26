﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Threading;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeAnalysis.CodeStyle
{
    // This part contains all the logic for hooking up the DiagnosticAnalyzer to the CodeStyleProvider.
    // All the code in this part is an implementation detail and is intentionally private so that
    // subclasses cannot change anything.  All code relevant to subclasses relating to analysis
    // is contained in AbstractCodeStyleProvider.cs

    internal abstract partial class AbstractCodeStyleProvider<TOptionKind, TCodeStyleProvider>
    {
        public abstract class DiagnosticAnalyzer : AbstractCodeStyleDiagnosticAnalyzer
        {
            public readonly TCodeStyleProvider _codeStyleProvider;

            protected DiagnosticAnalyzer(bool configurable = true) 
                : this(new TCodeStyleProvider(), configurable)
            {
            }

            private DiagnosticAnalyzer(TCodeStyleProvider codeStyleProvider, bool configurable)
                : base(codeStyleProvider._descriptorId, codeStyleProvider._title, codeStyleProvider._message, configurable)
            {
                _codeStyleProvider = codeStyleProvider;
            }

            protected sealed override void InitializeWorker(Diagnostics.AnalysisContext context)
                => _codeStyleProvider.DiagnosticAnalyzerInitialize(new AnalysisContext(_codeStyleProvider, context));

            public sealed override DiagnosticAnalyzerCategory GetAnalyzerCategory()
                => _codeStyleProvider.GetDiagnosticAnalyzerCategory();

            public sealed override bool OpenFileOnly(Workspace workspace)
                => _codeStyleProvider.DiagnosticsForOpenFileOnly(workspace);
        }

        protected struct AnalysisContext
        {
            private readonly TCodeStyleProvider _codeStyleProvider;
            private readonly Diagnostics.AnalysisContext _context;

            public AnalysisContext(TCodeStyleProvider codeStyleProvider, Diagnostics.AnalysisContext context)
            {
                _codeStyleProvider = codeStyleProvider;
                _context = context;
            }

            public void RegisterCodeBlockAction(Action<CodeBlockAnalysisContext, CodeStyleOption<TOptionKind>> analyze)
            {
                var provider = _codeStyleProvider;
                _context.RegisterCodeBlockAction(
                    c => AnalyzeIfEnabled(provider, c, analyze, c.Options, c.SemanticModel.SyntaxTree, c.CancellationToken));
            }

            public void RegisterSemanticModelAction(Action<SemanticModelAnalysisContext, CodeStyleOption<TOptionKind>> analyze)
            {
                var provider = _codeStyleProvider;
                _context.RegisterSemanticModelAction(
                    c => AnalyzeIfEnabled(provider, c, analyze, c.Options, c.SemanticModel.SyntaxTree, c.CancellationToken));
            }

            public void RegisterSsyntaxTreeAction(Action<SyntaxTreeAnalysisContext, CodeStyleOption<TOptionKind>> analyze)
            {
                var provider = _codeStyleProvider;
                _context.RegisterSyntaxTreeAction(
                    c => AnalyzeIfEnabled(provider, c, analyze, c.Options, c.Tree, c.CancellationToken));
            }

            public void RegisterOperationAction(
                Action<OperationAnalysisContext, CodeStyleOption<TOptionKind>> analyze,
                params OperationKind[] operationKinds)
            {
                var provider = _codeStyleProvider;
                _context.RegisterOperationAction(
                    c => AnalyzeIfEnabled(provider, c, analyze, c.Options, c.Operation.SemanticModel.SyntaxTree, c.CancellationToken),
                    operationKinds);
            }

            public void RegisterSyntaxNodeAction<TSyntaxKind>(
                Action<SyntaxNodeAnalysisContext, CodeStyleOption<TOptionKind>> analyze,
                params TSyntaxKind[] syntaxKinds) where TSyntaxKind : struct
            {
                var provider = _codeStyleProvider;
                _context.RegisterSyntaxNodeAction(
                    c => AnalyzeIfEnabled(provider, c, analyze, c.Options, c.SemanticModel.SyntaxTree, c.CancellationToken),
                    syntaxKinds);
            }

            static void AnalyzeIfEnabled<TContext>(
                TCodeStyleProvider provider, TContext context, Action<TContext, CodeStyleOption<TOptionKind>> analyze,
                AnalyzerOptions options, SyntaxTree syntaxTree, CancellationToken cancellationToken)
            {
                var optionSet = options.GetDocumentOptionSetAsync(syntaxTree, cancellationToken).GetAwaiter().GetResult();
                if (optionSet == null)
                {
                    return;
                }

                var optionValue = optionSet.GetOption(provider._option);
                var severity = GetOptionSeverity(optionValue);
                switch (severity)
                {
                    case ReportDiagnostic.Error:
                    case ReportDiagnostic.Warn:
                    case ReportDiagnostic.Info:
                        break;
                    default:
                        // don't analyze if it's any other value.
                        return;
                }

                analyze(context, optionValue);
            }
        }
    }
}
