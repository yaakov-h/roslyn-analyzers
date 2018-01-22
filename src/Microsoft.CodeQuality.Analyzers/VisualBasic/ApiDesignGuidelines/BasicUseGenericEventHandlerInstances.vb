﻿' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Linq
Imports Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines

Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Diagnostics
Imports Microsoft.CodeAnalysis.VisualBasic

Namespace Microsoft.CodeQuality.VisualBasic.Analyzers.ApiDesignGuidelines
    <DiagnosticAnalyzer(LanguageNames.VisualBasic)>
    Public Class BasicUseGenericEventHandlerInstancesAnalyzer
        Inherits UseGenericEventHandlerInstancesAnalyzer

    End Class
End Namespace
