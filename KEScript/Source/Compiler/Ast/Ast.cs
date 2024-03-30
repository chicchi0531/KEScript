namespace KESCompiler.Compiler.Ast;

public interface IAstWalker<out T>
{
    T Visit(AstNode astNode);

    T Visit(ProgramNode node);
    T Visit(ClassDecl decl);
    T Visit(FunctionDecl decl);
    T Visit(VarDecl decl);

    T Visit(BlockStmt stmt);
    T Visit(IfStmt stmt);
    T Visit(WhileStmt stmt);
    T Visit(ForStmt stmt);
    T Visit(ReturnStmt stmt);
    T Visit(BreakStmt stmt);
    T Visit(ContinueStmt stmt);

    T Visit(VarDeclStmt stmt);
    T Visit(ExprStmt stmt);
        
    T Visit(ConditionExpr expr);
    T Visit(AssignExpr expr);
    T Visit(BinaryExpr expr);
    T Visit(LogicExpr expr);
    T Visit(UnaryExpr expr);
    T Visit(PrimaryExpr expr);
        
    T Visit(IntegerLiteral integerLiteral);
    T Visit(FloatLiteral floatLiteral);
    T Visit(BooleanLiteral booleanLiteral);
    T Visit(StringLiteral stringLiteral);
    T Visit(NullLiteral nullLiteral);
    T Visit(PredefinedTypeName predefinedTypeName);
    T Visit(IdentifierName identifierName);
    T Visit(ModuleName moduleName);
    T Visit(FunctionCallExpr functionCallExpr);
    T Visit(ObjectCreationExpr objectCreationExpr);
}

public abstract class AstNode(CodePosition position)
{
    public CodePosition Position { get; } = position; //デバッグ用　元のコードの位置
    public abstract T Solve<T>(IAstWalker<T> walker);
}

//------------------------
// トップレベルのAST
//------------------------
public class ProgramNode
    (CodePosition position, ClassDecl[] classes, FunctionDecl[] globalFunctions, VarDecl[] globalVariables)
    : AstNode(position)
{
    public ClassDecl[] Classes { get; } = classes;
    public FunctionDecl[] GlobalFunctions { get; } = globalFunctions;
    public VarDecl[] GlobalVariables { get; } = globalVariables;
    public override T Solve<T>(IAstWalker<T> walker)
    {
        return walker.Visit(this);
    }

     ClassDecl? FindClass(string name) => Classes.FirstOrDefault(x => x.Name == name);
     FunctionDecl? FindFunction(string name) => GlobalFunctions.FirstOrDefault(x => x.Name == name);
     VarDecl? FindVariable(string name) => GlobalVariables.FirstOrDefault(x => x.Name == name);

     int GetClassHandle(string name) => Array.FindIndex(Classes, x => x.Name == name);
     int GetFunctionHandle(string name) => Array.FindIndex(GlobalFunctions, x => x.Name == name);
     int GetVariableAddress(string name) => Array.FindIndex(GlobalVariables, x => x.Name == name);
}

public enum EAccessModifier
{
    Public,
    Private,
    Protected,
}

public abstract class Decl(CodePosition position) : AstNode(position)
{
    public EAccessModifier AccessModifier { get; set; } = EAccessModifier.Public;
}

public class ClassDecl : Decl
{
    public ClassDecl(string name, EAccessModifier accessModifier, Expr? superClass, CodePosition position,
        FunctionDecl[] functions, VarDecl[] variables) : base(position)
    {
        Name = name;
        AccessModifier = accessModifier;
        SuperClass = superClass;
        Functions = functions;
        Variables = variables;
    }

    public string Name { get; }
    public Expr? SuperClass { get; }
    public FunctionDecl[] Functions { get; }
    public VarDecl[] Variables { get; }

    public override T Solve<T>(IAstWalker<T> walker)
    {
        return walker.Visit(this);
    }

    FunctionDecl? FindMethod(string name)
    {
        return Functions.FirstOrDefault(x => x.Name == name);
    }
     VarDecl? FindField(string name)
    {
        return Variables.FirstOrDefault(x => x.Name == name);
    }
     int GetMethodIndexOf(string name) => Array.FindIndex(Functions, x => x.Name == name);
     int GetFieldIndexOf(string name) => Array.FindIndex(Variables, x => x.Name == name);
}

public class FunctionDecl : Decl
{
    public FunctionDecl(EAccessModifier accessModifier, Expr returnType, string name, VarDecl[] args, BlockStmt body, CodePosition position) : base(position)
    {
        ReturnType = returnType;
        Name = name;
        Args = args;
        Body = body;
        AccessModifier = accessModifier;
    }
    public Expr ReturnType { get; }
    public string Name { get; }
    public VarDecl[] Args { get; }
    public BlockStmt Body { get; }

    public override T Solve<T>(IAstWalker<T> walker)
    {
        return walker.Visit(this);
    }
}

public class VarDecl : Decl
{    
    public VarDecl(EAccessModifier accessModifier, Expr type, string name, Expr initValue, bool isImmutable,
        int address, EVarScope scope, CodePosition position) : base(position)
    {
        Type = type;
        Name = name;
        InitValue = initValue;
        AccessModifier = accessModifier;
        IsImmutable = isImmutable;
        Address = address;
        VarScope = scope;
    }
        
    public bool IsImmutable { get; }
    public Expr? Type { get; }
    public string Name { get; }
    public Expr InitValue { get; }
    public int Address { get; }
    public EVarScope VarScope { get; }

    public override T Solve<T>(IAstWalker<T> walker)
    {
        return walker.Visit(this);
    }
}

public class ArgVarDecl : AstNode
{
    public ArgVarDecl(string typename, string name, CodePosition position) : base(position)
    {
        TypeName = typename;
        Name = name;
    }
    public string TypeName { get; }
    public string Name { get; }

    public override T Solve<T>(IAstWalker<T> walker)
    {
        return walker.Visit(this);
    }
}

//------------------------
// 文関連のAST
//------------------------
public abstract class Stmt(CodePosition position) : AstNode(position);

public class BlockStmt(Stmt[] stmts, CodePosition position) : Stmt(position)
{

    public Stmt[] Stmts { get; } = stmts;
    public override T Solve<T>(IAstWalker<T> walker) => walker.Visit(this);
}

public class ExprStmt(Expr expr, CodePosition position) : Stmt(position)
{
    public Expr Expr { get; } = expr;
    public override T Solve<T>(IAstWalker<T> walker) => walker.Visit(this);
}

public class VarDeclStmt(VarDecl varDecl, CodePosition position) : Stmt(position)
{
    public VarDecl VarDecl { get; } = varDecl;
    public override T Solve<T>(IAstWalker<T> walker) => walker.Visit(this);
}

public class WhileStmt(ConditionExpr condition, Stmt body, CodePosition position) : Stmt(position)
{
    public ConditionExpr Condition { get; } = condition;
    public Stmt Body { get; } = body;
    public override T Solve<T>(IAstWalker<T> walker) => walker.Visit(this);
}

public class ForStmt(Stmt init, ConditionExpr condition, Expr update, Stmt body, CodePosition position)
    : Stmt(position)
{
    public Stmt Init { get; } = init;
    public ConditionExpr Condition { get; } = condition;
    public Expr Update { get; } = update;
    public Stmt Body { get; } = body;
    public override T Solve<T>(IAstWalker<T> walker) => walker.Visit(this);
}

public class IfStmt(ConditionExpr condition, Stmt thenBody, Stmt elseBody, CodePosition position)
    : Stmt(position)
{
    public ConditionExpr Condition { get; } = condition;
    public Stmt ThenBody { get; } = thenBody;
    public Stmt ElseBody { get; } = elseBody;

    public override T Solve<T>(IAstWalker<T> walker) => walker.Visit(this);
}

public class ReturnStmt(Expr returnExpr, CodePosition position) : Stmt(position)
{
    public Expr ReturnExpr { get; } = returnExpr;
    public override T Solve<T>(IAstWalker<T> walker) => walker.Visit(this);
}

public class BreakStmt(CodePosition position) : Stmt(position)
{
    public override T Solve<T>(IAstWalker<T> walker) => walker.Visit(this);
}

public class ContinueStmt(CodePosition position) : Stmt(position)
{
    public override T Solve<T>(IAstWalker<T> walker) => walker.Visit(this);
}

//-------------------------
// 式関連のAST
//-------------------------
public abstract class Expr(CodePosition position) : AstNode(position)
{
    public virtual bool Equals(Expr expr) => throw new NotImplementedException();
}

public class ConditionExpr(Expr expr, CodePosition position) : Expr(position)
{
    public Expr Expr { get; } = expr;
    public override T Solve<T>(IAstWalker<T> walker) => walker.Visit(this);
}

public class AssignExpr(Expr lValue, Expr rValue, CodePosition position) : Expr(position)
{
    public Expr LValue { get; } = lValue;
    public Expr RValue { get; set; } = rValue; //型変換でコンバートされる場合があるので、setアクセサを用意
    public override T Solve<T>(IAstWalker<T> walker) => walker.Visit(this);
}

public class ConvertExpr(Expr typename, Expr value, CodePosition position) : Expr(position)
{
    public Expr TypeName { get; } = typename;
    public Expr Value { get; } = value;
    public override T Solve<T>(IAstWalker<T> walker) => walker.Visit(this);
}

public class BinaryExpr(Expr left, Token op, Expr right, CodePosition position)
    : Expr(position)
{
    public Expr Left { get; set; } = left; //型変換でコンバートされる場合があるので、setアクセサを用意
    public KesOperator Op { get; } = KesOperatorHelper.TokenToBinaryOperator(op);
    public Expr Right { get; set;  } = right; //型変換でコンバートされる場合があるので、setアクセサを用意

    public override T Solve<T>(IAstWalker<T> walker) => walker.Visit(this);
}

public class LogicExpr(Expr left, Token op, Expr right, CodePosition position)
    : Expr(position)
{

    public Expr Left { get; } = left;
    public KesOperator Op { get; } = KesOperatorHelper.TokenToLogicOperator(op);
    public Expr Right { get; } = right;

    public override T Solve<T>(IAstWalker<T> walker) => walker.Visit(this);
}

public class UnaryExpr(Expr expr, Token op, CodePosition position) : Expr(position)
{
    public Expr Expr { get; } = expr;
    public KesOperator Op { get; } = KesOperatorHelper.TokenToUnaryOperator(op);

    public override T Solve<T>(IAstWalker<T> walker) => walker.Visit(this);
}
    
public class PrimaryExpr(Expr expr, Token op, CodePosition position) : Expr(position)
{
    public Expr Expr { get; } = expr;
    public KesOperator Op { get; } = KesOperatorHelper.TokenToPrimaryOperator(op);

    public override T Solve<T>(IAstWalker<T> walker) => walker.Visit(this);
}

// 終端記号　即値
public class IntegerLiteral(int value, CodePosition position) : Expr(position)
{

    public int Value { get; } = value;

    public override T Solve<T>(IAstWalker<T> walker) => walker.Visit(this);
}
public class FloatLiteral(float value, CodePosition position) : Expr(position)
{
    public float Value { get; } = value;

    public override T Solve<T>(IAstWalker<T> walker) => walker.Visit(this);
}
public class BooleanLiteral(bool value, CodePosition position) : Expr(position)
{
    public bool Value { get; } = value;
    public override T Solve<T>(IAstWalker<T> walker) => walker.Visit(this);
}
public class StringLiteral(string value, CodePosition position) : Expr(position)
{
    public string Value { get; } = value;
    public override T Solve<T>(IAstWalker<T> walker) => walker.Visit(this);
}
public class NullLiteral(CodePosition position) : Expr(position)
{
    public override T Solve<T>(IAstWalker<T> walker) => walker.Visit(this);
}
    
// 終端記号　識別子
public class IdentifierName(string name, CodePosition position) : Expr(position)
{
    public string Name { get; } = name;
    public override bool Equals(Expr expr) => Name == (expr as IdentifierName)?.Name;
    public override T Solve<T>(IAstWalker<T> walker) => walker.Visit(this);
}
    
// 終端記号　修飾名
public class ModuleName(Expr left, IdentifierName right, CodePosition position) : Expr(position)
{
    public Expr Left { get; } = left;
    public IdentifierName Right { get; } = right;
    public override bool Equals(Expr expr)
    {
        if (expr is not ModuleName q) return false;
        return Right.Equals(q.Right) && Left.Equals(q.Left);
    }
    public override T Solve<T>(IAstWalker<T> walker) => walker.Visit(this);
}
    
// 終端記号 組み込み型
public class PredefinedTypeName(Token t, CodePosition position) : Expr(position)
{
    public Token Token { get; } = t;

    public override bool Equals(Expr expr)
    {
        if(expr is not PredefinedTypeName p) return false;
        return p.Token.Type == Token.Type;
    }

    public override T Solve<T>(IAstWalker<T> walker) => walker.Visit(this);

    public bool IsBool => Token.Type == ETokenType.Bool;
    public bool IsInt => Token.Type == ETokenType.Int;
    public bool IsFloat => Token.Type == ETokenType.Float;
    public bool IsString => Token.Type == ETokenType.String;
}

// 関数呼び出し
public class FunctionCallExpr(Expr expr, List<Expr> args, CodePosition position) : Expr(position)
{

    public Expr Expr { get; } = expr;
    public List<Expr> Args { get; } = args;

    public override T Solve<T>(IAstWalker<T> walker) => walker.Visit(this);
}
    
public class MemberAccessExpr(string name, Expr expr, CodePosition position) : Expr(position)
{
    public string Name { get; } = name;
    public Expr Expr { get;} = expr;

    public override T Solve<T>(IAstWalker<T> walker) => walker.Visit(this);
}

public class ObjectCreationExpr(Expr typeName, List<Expr> args, CodePosition position) : Expr(position)
{
    public Expr TypeName { get; } = typeName;
    public List<Expr> Args { get; } = args;

    public override T Solve<T>(IAstWalker<T> walker) => walker.Visit(this);
}

public enum KesOperator
{
    // binary operator
    Add, // x + y
    Sub, // x - y
    Mul, // x * y
    Div, // x / y
    Mod, // x % y
    Eq,     // x == y
    NotEq,  // x != y
    Lt,     // x < y
    Le,     // x <= y
    Gt,     // x > y
    Ge,     // x >= y
    LogAnd, // x & y
    LogOr,  // x | y
        
    // logic operator
    Or,     // x || y
    And,    // x && y

    // unary operator
    Not,    // !x
    Minus,  // -x
    PreIncr, // ++x
    PreDecr, // --x
    PostIncr, // x++
    PostDecr // x--
}
    
internal static class KesOperatorHelper
{
    public static string ToString(KesOperator op)
    {
        return op switch
        {
            KesOperator.Add => "+",
            KesOperator.Sub => "-",
            KesOperator.Mul => "*",
            KesOperator.Div => "/",
            KesOperator.Mod => "%",
            KesOperator.Eq => "==",
            KesOperator.NotEq => "!=",
            KesOperator.Lt => "<",
            KesOperator.Le => "<=",
            KesOperator.Gt => ">",
            KesOperator.Ge => ">=",
            KesOperator.LogAnd => "&",
            KesOperator.LogOr => "|",
            KesOperator.Or => "||",
            KesOperator.And => "&&",
            KesOperator.Not => "!",
            KesOperator.Minus => "-",
            KesOperator.PreIncr => "/++",
            KesOperator.PreDecr => "/--",
            KesOperator.PostIncr => "++/",
            KesOperator.PostDecr => "--/",
            _ => throw new ArgumentOutOfRangeException(nameof(op), op, null)
        };
    }
        
    public static KesOperator TokenToBinaryOperator(Token token, bool isPost=false)
    {
        return token.Type switch
        {
            // binary operator
            ETokenType.Plus => KesOperator.Add,
            ETokenType.Minus => KesOperator.Sub,
            ETokenType.Asterisk => KesOperator.Mul,
            ETokenType.Slash => KesOperator.Div,
            ETokenType.Percent => KesOperator.Mod, 
            ETokenType.Equal => KesOperator.Eq,
            ETokenType.NotEqual => KesOperator.NotEq,
            ETokenType.Less => KesOperator.Lt,
            ETokenType.LessEqual => KesOperator.Le,
            ETokenType.Greater => KesOperator.Gt,
            ETokenType.GreaterEqual => KesOperator.Ge,
            _ => throw new InvalidCastException()
        };
    }
        
    public static KesOperator TokenToLogicOperator(Token token)
    {
        return token.Type switch
        {
            ETokenType.And => KesOperator.And,
            ETokenType.Or => KesOperator.Or,
            _ => throw new InvalidCastException()
        };
    }
        
    public static KesOperator TokenToUnaryOperator(Token token)
    {
        return token.Type switch
        {
            ETokenType.Not => KesOperator.Not, 
            ETokenType.Minus => KesOperator.Minus, 
            ETokenType.PlusPlus => KesOperator.PreIncr,
            ETokenType.MinusMinus => KesOperator.PreDecr,
            _ => throw new InvalidCastException()
        };
    }
        
    public static KesOperator TokenToPrimaryOperator(Token token)
    {
        return token.Type switch
        {
            ETokenType.PlusPlus => KesOperator.PostIncr,
            ETokenType.MinusMinus => KesOperator.PostDecr,
            _ => throw new InvalidCastException()
        };
    }
}