using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace KEventSourceGeneration;

[Generator]
public class ErrorCodeGenerator : ISourceGenerator
{
    const string _attributeName = "KesErrorAttribute";
    const string _errorSturctName = "KesErrorType";
    public void Execute(GeneratorExecutionContext context)
    {
        var receiver = context.SyntaxReceiver as ClassSyntaxReceiver;
        if (receiver is null) return;

        StringBuilder source = new StringBuilder();
        //source.Append(ErrorDeclaration);

        foreach (var c in receiver.ClassDeclarations)
        {
            var model = context.Compilation.GetSemanticModel(c.SyntaxTree);
            var symbol = model.GetDeclaredSymbol(c) as INamedTypeSymbol;
            var name = symbol.Name;
            var attributes = symbol.GetAttributes()
                .Where(x => x.AttributeClass.Name == _attributeName)
                .Select(x => (
                    ID: (int)x.ConstructorArguments[0].Value,
                    Name: x.ConstructorArguments[1].Value.ToString(),
                    Message: x.ConstructorArguments[2].Value.ToString()
                ))
                .ToArray();
            if (attributes.Length == 0) continue;

            var codes = CreateProperty(context, symbol, attributes);
            source.Append(codes);
        }
        context.AddSource("KesError.g.cs", SourceText.From(source.ToString(), Encoding.UTF8));
    }

    public void Initialize(GeneratorInitializationContext context)
    {
        context.RegisterForSyntaxNotifications(() => new ClassSyntaxReceiver());

    }

    string ErrorDeclaration => $@"
using System;
namespace Koromosoft.KEventSystem.Core
{{
    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = true)]
    public sealed class KesErrorAttribute : Attribute
    {{
        {_errorSturctName} _errorInfo;
        public KesErrorAttribute(int id, string name, string message) {{_errorInfo = new (id, name, message);}}
    }}
    
    public struct {_errorSturctName}
    {{
        public {_errorSturctName}(int id, string name, string message){{
            this.id = id;
            this.name = name;
            this.message = message;
        }}
        public int id;
        public string name;
        public string message;
    }}
}}
";

    string CreateProperty(GeneratorExecutionContext context, INamedTypeSymbol classSymbol, (int ID, string Name, string Message)[] attributes)
    {
        //TODO:属性のプロパティ化
        var className = classSymbol.Name;
        var builder = new StringBuilder($@"
namespace {classSymbol.ContainingNamespace.ToDisplayString()}
{{
    public static partial class {className}
    {{
            ");
            
        foreach(var attr in attributes)
        {
            builder.Append($@"
        public static readonly {_errorSturctName} {attr.Name} = new ({attr.ID},""{attr.Name}"", ""{attr.Message}"");
                ");
        }

        builder.Append(@"
    }}
            ");
        return builder.ToString();
    }
}

class ClassSyntaxReceiver : ISyntaxReceiver
{
    public List<ClassDeclarationSyntax> ClassDeclarations { get; } = new List<ClassDeclarationSyntax>();
    public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
    {
        if(syntaxNode is ClassDeclarationSyntax c && c.AttributeLists
            .SelectMany(x => x.Attributes)
            .Select(x => x.Name.NormalizeWhitespace().ToFullString().Split('.').Last())
            .Contains("KesError"))
        {
            ClassDeclarations.Add(c);
        }
    }
}