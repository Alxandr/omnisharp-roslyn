﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.Mvc;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Recommendations;
using Microsoft.CodeAnalysis.Text;
using OmniSharp.Models;

namespace OmniSharp
{
    public partial class OmnisharpController
    {
        [HttpPost("autocomplete")]
        public async Task<IActionResult> AutoComplete([FromBody]AutoCompleteRequest request)
        {
            _workspace.EnsureBufferUpdated(request);

            var completions = new List<AutoCompleteResponse>();

            var documents = _workspace.GetDocuments(request.FileName);

            foreach (var document in documents)
            {
                var sourceText = await document.GetTextAsync();
                var position = sourceText.Lines.GetPosition(new LinePosition(request.Line - 1, request.Column - 1));
                var model = await document.GetSemanticModelAsync();
                var symbols = Recommender.GetRecommendedSymbolsAtPosition(model, position, _workspace);

                foreach (var symbol in symbols.Where(s => s.Name.StartsWith(request.WordToComplete, StringComparison.OrdinalIgnoreCase)))
                {
                    completions.Add(MakeAutoCompleteResponse(request, symbol));
                    var typeSymbol = symbol as INamedTypeSymbol;
                    if (typeSymbol != null)
                    {
                        foreach (var ctor in typeSymbol.InstanceConstructors)
                        {
                            completions.Add(MakeAutoCompleteResponse(request, ctor));
                        }
                    }
                }
            }

            return new ObjectResult(completions);
        }

        private AutoCompleteResponse MakeAutoCompleteResponse(AutoCompleteRequest request, ISymbol symbol)
        {
            var response = new AutoCompleteResponse();
            response.CompletionText = symbol.Name;

            // TODO: Do something more intelligent here
            response.DisplayText = symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

            if (request.WantDocumentationForEveryCompletionResult)
            {
                response.Description = symbol.GetDocumentationCommentXml();
            }

            if (request.WantReturnType)
            {
                var methodSymbol = symbol as IMethodSymbol;
                if (methodSymbol != null)
                {
                    if (methodSymbol.MethodKind != MethodKind.Constructor)
                    {
                        response.ReturnType = methodSymbol.ReturnType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                    }
                }
                else
                {
                    var propertySymbol = symbol as IPropertySymbol;
                    if (propertySymbol != null)
                    {
                        response.ReturnType = propertySymbol.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                    }

                    var localSymbol = symbol as ILocalSymbol;
                    if (localSymbol != null)
                    {
                        response.ReturnType = localSymbol.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                    }

                    var parameterSymbol = symbol as IParameterSymbol;
                    if (parameterSymbol != null)
                    {
                        response.ReturnType = parameterSymbol.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                    }

                    var fieldSymbol = symbol as IFieldSymbol;
                    if (fieldSymbol != null)
                    {
                        response.ReturnType = fieldSymbol.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                    }
                }
            }

            if (request.WantSnippet)
            {
                response.Snippet = new SnippetGenerator(true).GenerateSnippet(symbol);
            }

            if (request.WantMethodHeader)
            {
                response.MethodHeader = new SnippetGenerator(false).GenerateSnippet(symbol);
            }

            return response;
        }
    }
}