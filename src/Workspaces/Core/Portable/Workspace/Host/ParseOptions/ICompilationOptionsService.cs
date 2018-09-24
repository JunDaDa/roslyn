﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

namespace Microsoft.CodeAnalysis.Host
{
    internal interface ICompilationOptionsService : ILanguageService
    {
        bool SupportsUnsafe { get; }
        bool GetAllowUnsafe(CompilationOptions options);
        CompilationOptions WithAllowUnsafe(CompilationOptions old, bool allowUnsafe);
    }
}
