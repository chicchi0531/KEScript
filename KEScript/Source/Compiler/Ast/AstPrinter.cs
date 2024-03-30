using System.Globalization;
using System.Text;

namespace KESCompiler.Compiler.Ast;

public class AstPrinter : IAstWalker<string>
{
    StringBuilder _str = new(4096);
    int _indent = 0;
        
    public string Visit(AstNode astNode)
    {
        throw new NotImplementedException(astNode.GetType().ToString());
    }
    public string Visit(ProgramNode node)
    {
        _str.Append($"Program {node.Position.filename}\n");
        _indent++;
        foreach (var d in node.GlobalVariables)
        {
            d.Solve(this);
        }
        foreach (var d in node.GlobalFunctions)
        {
            d.Solve(this);
        }
        foreach (var d in node.Classes)
        {
            d.Solve(this);
        }
        return _str.ToString();
    }
    public string Visit(ClassDecl decl)
    {
        var superclasses = "";
        foreach (var s in decl.SuperClasses)
        {
            superclasses += s + ", ";
        }
        Print($"ClassDecl: {decl.Position} {decl.Name} super({superclasses})");
        _indent++;
        foreach (var d in decl.Variables)
        {
            d.Solve(this);
        }
        foreach (var d in decl.Functions)
        {
            d.Solve(this);
        }
        _indent--;
        return null;
    }
    public string Visit(FunctionDecl decl)
    {
        var args = "";
        foreach (var a in decl.Args)
        {
            args += a.Name + ":" + a.Type + " ";
        }
        Print($"FuncDecl: {decl.Position} {decl.Name} ({args}) -> {decl.ReturnType}");
        _indent++;
        foreach(var s in decl.Body)
        {
            s.Solve(this);
        }
        _indent--;
        return null;
    }
    public string Visit(VarDecl decl)
    {
        if (decl.IsImmutable)
        {
            Print($"LetDecl: {decl.Position} {decl.Name}:{decl.Type}");
        }
        else
        {
            Print($"VarDecl: {decl.Position} {decl.Name}:{decl.Type}");
        }
            
        if(decl.InitValue!= null)
        {
            _indent++;
            Print($"Init: {decl.InitValue.Position} {decl.InitValue.Solve(this)}");
            _indent--;
        }
        return null;
    }
    public string Visit(BlockStmt stmt)
    {
        foreach(var s in stmt.Stmts) s.Solve(this);
        return null;
    }
    public string Visit(IfStmt stmt)
    {
        Print($"If: {stmt.Position} {stmt.Condition.Solve(this)}");
        _indent++;
        foreach (var s in stmt.ThenBody)
        {
            s.Solve(this);
        }
        _indent--;
            
        if(stmt.ElseBody.Length > 0)
        {
            Print($"Else:");
            _indent++;
            foreach (var s in stmt.ElseBody)
            {
                s.Solve(this);
            }
            _indent--;
        }
        return null;
    }
    public string Visit(WhileStmt stmt)
    {
        Print($"While: {stmt.Position} {stmt.Condition.Solve(this)}");
        _indent++;
        foreach (var s in stmt.Body)
        {
            s.Solve(this);
        }
        _indent--;
        return null;
    }
    public string Visit(ForStmt stmt)
    {
        Print($"For: {stmt.Position} {stmt.Init.Solve(this)} {stmt.Condition.Solve(this)} {stmt.Update.Solve(this)}");
        _indent++;
        foreach (var s in stmt.Body)
        {
            s.Solve(this);
        }
        _indent--;
        return null;
    }
    public string Visit(ReturnStmt stmt)
    {
        Print($"Return: {stmt.Position} {stmt.ReturnExpr.Solve(this)}");
        return null;
    }
    public string Visit(BreakStmt stmt)
    {
        Print("Break:");
        return null;
    }
    public string Visit(ContinueStmt stmt)
    {
        Print("Continue:");
        return null;
    }
    public string Visit(VarDeclStmt stmt)
    {
        stmt.VarDecl.Solve(this);
        return null;
    }
    public string Visit(ExprStmt stmt)
    {
        Print($"Expr: {stmt.Position} {stmt.Expr.Solve(this)}");
        return null;
    }
    public string Visit(ConditionExpr expr) => throw new NotImplementedException();
    public string Visit(AssignExpr expr)
    {
        return $"{expr.LValue.Name}<{expr.LValue.TypeName}> = {expr.RValue.Solve(this)}<{expr.RValue.TypeName}>";
    }
    public string Visit(BinaryExpr expr)
    {
        var op = KesOperatorHelper.ToString(expr.Op);
        return $"({op} {expr.Left.Solve(this)} {expr.Right.Solve(this)})<{expr.TypeName}>";
    }
    public string Visit(LogicExpr expr)
    {
        var op = KesOperatorHelper.ToString(expr.Op);
        return $"({op} {expr.Left.Solve(this)} {expr.Right.Solve(this)})<{expr.TypeName}>";
    }
    public string Visit(UnaryExpr expr)
    {
        var op = KesOperatorHelper.ToString(expr.Op);
        return $"({op} {expr.Expr.Solve(this)})<{expr.TypeName}>";
    }
    public string Visit(PrimaryExpr expr)
    {
        var op = KesOperatorHelper.ToString(expr.Op);
        return $"({op} {expr.Expr.Solve(this)})<{expr.TypeName}>";
    }
    public string Visit(IntegerLiteral integerLiteral) => integerLiteral.Value.ToString();
    public string Visit(FloatLiteral floatLiteral) => floatLiteral.Value.ToString(CultureInfo.InvariantCulture);
    public string Visit(BooleanLiteral booleanLiteral) => booleanLiteral.Value.ToString();
    public string Visit(StringLiteral stringLiteral) => stringLiteral.Value.ToString();
    public string Visit(NullLiteral nullLiteral) => "null";
    public string Visit(PredefinedTypeName predefinedTypeName) => predefinedTypeName.TypeName;
    public string Visit(IdentifierName identifierName)
    {
        var stringBuilder = new StringBuilder(128);

        stringBuilder.Append(identifierName.Name);

        if (identifierName.Next != null)
        {
            stringBuilder.Append($".{identifierName.Next.Solve(this)}<{identifierName.TypeName}>");
        }
            
        return stringBuilder.ToString();
    }
    public string Visit(ModuleName moduleName) => throw new NotImplementedException();
    public string Visit(FunctionCallExpr functionCallExpr)
    {
        var f = functionCallExpr;
        return $"{f.Expr}({string.Join(", ", f.Args.Select(a => a.Solve(this)))})<{functionCallExpr.TypeName}>";
    }
    public string Visit(MemberAccessExpr memberAccessExpr)
    {
        var stringBuilder = new StringBuilder(128);

        stringBuilder.Append(memberAccessExpr.Name);

        if (memberAccessExpr.Next != null)
        {
            stringBuilder.Append($".{memberAccessExpr.Next.Solve(this)}<{memberAccessExpr.TypeName}>");
        }
            
        return stringBuilder.ToString();
    }
    public string Visit(ObjectCreationExpr objectCreationExpr)
    {
        return $"new {objectCreationExpr.TypeName}({string.Join(", ", objectCreationExpr.Args.Select(a => a.Solve(this)))})<{objectCreationExpr.TypeName}>";
    }

    void Print(string message)
    {
        _str.Append(' ', _indent * 3).Append($"└─ {message}\n");
    }
}