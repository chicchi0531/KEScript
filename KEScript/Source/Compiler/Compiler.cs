using System.Text;
using KESCompiler.Compiler.Ast;

namespace KESCompiler.Compiler;

public struct CompilerMetaData
{
    public string filepath;
}

public enum ECompilerOption : byte
{
    PrintTokens = 0x01,
    PrintAst = 0x02,
    PrintOpCodes = 0x04,
}
    
public class Compiler(ILogger logger)
{
    ILogger _logger = logger;

    // 引数に渡したASTを捜査し、目的コードを生成する
    public byte[]? Compile(string source, CompilerMetaData metaData, byte option)
    {
        // lexing
        var lexer = new Lexer(_logger);
        var tokens = lexer.Lex(source, metaData.filepath);
        if ((option & (byte)ECompilerOption.PrintTokens) > 0)
        {
            PrintTokens(tokens);
        }
        if (_logger.ErrorCount > 0) return null;
            
        // persing
        var parser = new Parser(_logger);
        var ast = parser.Parse(tokens);
        if(_logger.ErrorCount > 0) return null;
        if ((option & (byte)ECompilerOption.PrintAst) > 0)
        {
            PrintAst(ast);
        }
            
        // ast path1 : シンボルの解決
        var symbolResolver = new SymbolResolver(_logger, ast);
        var hir = symbolResolver.Resolve();
        if(_logger.ErrorCount > 0) return null;
        if ((option & (byte)ECompilerOption.PrintAst) > 0)
        {
            PrintAst(ast);
        }
            
        // 型解決
            
        // ast path2 : 目的コード生成
        var codeGenerator = new CodeGenerator(_logger, hir);
        var result = codeGenerator.Generate();
        PrintOpCodes(result);
            
        return null;
    }
        
    void PrintTokens(Token[] tokens)
    {
        var str = new StringBuilder(1024);
        foreach (var t in tokens)
        {
            str.AppendFormat("({0}:{1}) {2} ({3}) \"{4}\"\n",t.Position.line, t.Position.count, t.Type, t.Lexeme, t.Source);
        }
        _logger.Log(str.ToString());
    }

    void PrintAst(AstNode astNodeNode)
    {
        var printer = new AstPrinter();
        var result = new StringBuilder(1024);
        result.Append(astNodeNode.Solve(printer)); //結果の出力
        _logger.Log(result.ToString());
    }
        
    void PrintOpCodes(List<Op> list)
    {
        var str = new StringBuilder(1024);
        foreach (var item in list)
        {
            str.AppendFormat("[L:{0}] {1} operand:{{ ", item.codePos.line, ((EOpCode)item.opCode).ToString());
                
            if(item.operand is not null) 
                foreach(var o in item.operand) str.AppendFormat("[{0:X2}] ", o);
            str.Append("}\n");
        }
        _logger.Log(str.ToString());
    }
        
}