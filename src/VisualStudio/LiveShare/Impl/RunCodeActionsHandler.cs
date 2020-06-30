﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.Editor.Shared.Utilities;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.LanguageServer;
using Microsoft.CodeAnalysis.LanguageServer.CustomProtocol;
using Microsoft.CodeAnalysis.LanguageServer.Handler;
using Microsoft.CodeAnalysis.LanguageServer.Handler.Commands;
using Newtonsoft.Json.Linq;
using LSP = Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.VisualStudio.LanguageServices.LiveShare
{
    /// <summary>
    /// Runs code actions as a command on the server.
    /// Commands must be applied from the UI thread in VS.
    /// </summary>
    [ExportExecuteWorkspaceCommand(CodeActionsHandler.RunCodeActionCommandName)]
    internal class RunCodeActionsHandler : IExecuteWorkspaceCommandHandler
    {
        private readonly ICodeFixService _codeFixService;
        private readonly ICodeRefactoringService _codeRefactoringService;
        private readonly ILspSolutionProvider _solutionProvider;
        private readonly IThreadingContext _threadingContext;

        [ImportingConstructor]
        [Obsolete(MefConstruction.ImportingConstructorMessage, error: true)]
        public RunCodeActionsHandler(
            ICodeFixService codeFixService,
            ICodeRefactoringService codeRefactoringService,
            ILspSolutionProvider solutionProvider,
            IThreadingContext threadingContext)
        {
            _codeFixService = codeFixService;
            _codeRefactoringService = codeRefactoringService;
            _solutionProvider = solutionProvider;
            _threadingContext = threadingContext;
        }

        public async Task<object> HandleRequestAsync(
            LSP.ExecuteCommandParams request,
            LSP.ClientCapabilities clientCapabilities,
            CancellationToken cancellationToken)
        {
            var runRequest = ((JToken)request.Arguments.Single()).ToObject<RunCodeActionParams>();
            var document = _solutionProvider.GetDocument(runRequest.CodeActionParams.TextDocument);
            var codeActions = await CodeActionsHandler.GetCodeActionsAsync(document, _codeFixService, _codeRefactoringService,
                runRequest.CodeActionParams.Range, cancellationToken).ConfigureAwait(false);
            if (codeActions == null)
            {
                return false;
            }

            var actionToRun = CodeActionResolveHandler.GetCodeActionToResolve(runRequest.DistinctTitle, codeActions);
            if (actionToRun != null)
            {
                foreach (var operation in await actionToRun.GetOperationsAsync(cancellationToken).ConfigureAwait(false))
                {
                    // TODO - This UI thread dependency should be removed.
                    // https://github.com/dotnet/roslyn/projects/45#card-20619668
                    await _threadingContext.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
                    operation.Apply(document.Project.Solution.Workspace, cancellationToken);
                }

                return true;
            }

            return false;
        }
    }
}
