﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.ValueContentAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Security
{
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class UseSharedAccessProtocolHttpsOnly : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "CA5376";
        private static readonly LocalizableString s_Title = new LocalizableResourceString(
            nameof(SystemSecurityCryptographyResources.UseSharedAccessProtocolHttpsOnly),
            SystemSecurityCryptographyResources.ResourceManager,
            typeof(SystemSecurityCryptographyResources));
        private static readonly LocalizableString s_Message = new LocalizableResourceString(
            nameof(SystemSecurityCryptographyResources.UseSharedAccessProtocolHttpsOnlyMessage),
            SystemSecurityCryptographyResources.ResourceManager,
            typeof(SystemSecurityCryptographyResources));
        private static readonly LocalizableString s_Description = new LocalizableResourceString(
            nameof(SystemSecurityCryptographyResources.UseSharedAccessProtocolHttpsOnlyDescription),
            SystemSecurityCryptographyResources.ResourceManager,
            typeof(SystemSecurityCryptographyResources));

        internal static DiagnosticDescriptor Rule = new DiagnosticDescriptor(
                DiagnosticId,
                s_Title,
                s_Message,
                DiagnosticCategory.Security,
                DiagnosticHelpers.DefaultDiagnosticSeverity,
                isEnabledByDefault: DiagnosticHelpers.EnabledByDefaultIfNotBuildingVSIX,
                description: s_Description,
                helpLinkUri: null,
                customTags: WellKnownDiagnosticTagsExtensions.DataflowAndTelemetry);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        /// <summary>
        /// SharedAccessProtocol.HttpsOnly = 1, SharedAccessProtocol.HttpsOrHttp = 2.
        /// </summary>
        private const int SharedAccessProtocolHttpsOnly = 1;

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();

            // Security analyzer - analyze and report diagnostics on generated code.
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

            context.RegisterCompilationStartAction(compilationStartAnalysisContext =>
            {
                var microsoftWindowsAzureStorageNamespaceSymbol = compilationStartAnalysisContext
                            .Compilation
                            .GlobalNamespace
                            .GetMembers("Microsoft")
                            ?.FirstOrDefault()
                            .GetMembers("WindowsAzure")
                            .OfType<INamespaceOrTypeSymbol>()
                            .FirstOrDefault()
                            .GetMembers("Storage")
                            .OfType<INamespaceOrTypeSymbol>()
                            .FirstOrDefault();

                if (microsoftWindowsAzureStorageNamespaceSymbol == null)
                {
                    return;
                }

                var wellKnownTypeProvider = WellKnownTypeProvider.GetOrCreate(compilationStartAnalysisContext.Compilation);

                wellKnownTypeProvider.TryGetTypeByMetadataName(
                                        WellKnownTypeNames.MicrosoftWindowsAzureStorageCloudStorageAccount,
                                        out INamedTypeSymbol cloudStorageAccountTypeSymbol);

                compilationStartAnalysisContext.RegisterOperationBlockStartAction(operationBlockStartContext =>
                {
                    var owningSymbol = operationBlockStartContext.OwningSymbol;

                    operationBlockStartContext.RegisterOperationAction(operationAnalysisContext =>
                    {
                        var invocationOperation = (IInvocationOperation)operationAnalysisContext.Operation;
                        var methodSymbol = invocationOperation.TargetMethod;

                        if (methodSymbol.Name != "GetSharedAccessSignature")
                        {
                            return;
                        }

                        var namespaceSymbol = methodSymbol.ContainingNamespace;

                        while (namespaceSymbol != null)
                        {
                            if (namespaceSymbol.Equals(microsoftWindowsAzureStorageNamespaceSymbol))
                            {
                                break;
                            }

                            namespaceSymbol = namespaceSymbol.ContainingNamespace;
                        }

                        if (namespaceSymbol == null)
                        {
                            return;
                        }

                        var typeSymbol = methodSymbol.ContainingType;

                        if (!typeSymbol.Equals(cloudStorageAccountTypeSymbol))
                        {
                            var protocolsArgumentOperation = invocationOperation.Arguments.FirstOrDefault(s => s.Parameter.Name == "protocols");

                            if (protocolsArgumentOperation != null)
                            {
                                var interproceduralAnalysisConfig = InterproceduralAnalysisConfiguration.Create(
                                                                        operationBlockStartContext.Options,
                                                                        SupportedDiagnostics,
                                                                        defaultInterproceduralAnalysisKind: InterproceduralAnalysisKind.None,
                                                                        cancellationToken: operationBlockStartContext.CancellationToken,
                                                                        defaultMaxInterproceduralMethodCallChain: 1);
                                var valueContentAnalysisResult = ValueContentAnalysis.GetOrComputeResult(
                                                                                            invocationOperation.GetTopmostParentBlock().GetEnclosingControlFlowGraph(),
                                                                                            owningSymbol,
                                                                                            wellKnownTypeProvider,
                                                                                            interproceduralAnalysisConfig,
                                                                                            out var copyAnalysisResult,
                                                                                            out var pointsToAnalysisResult);

                                var protocolsArgument = valueContentAnalysisResult[protocolsArgumentOperation.Kind, protocolsArgumentOperation.Syntax];

                                if (protocolsArgument.IsLiteralState &&
                                    !protocolsArgument.LiteralValues.Contains(SharedAccessProtocolHttpsOnly))
                                {
                                    operationAnalysisContext.ReportDiagnostic(
                                        invocationOperation.CreateDiagnostic(
                                            Rule));
                                }
                            }
                        }
                    }, OperationKind.Invocation);
                });
            });
        }
    }
}
