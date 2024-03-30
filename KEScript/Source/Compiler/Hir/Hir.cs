namespace KESCompiler.Compiler.Hir;

public interface IHirWalker<out T>
{
    T Visit(HirNode hirNode);
    T Visit(ProgramNode programNode);
    T Visit(ClassDecl classDecl);
    T Visit(FunctionDecl functionDecl);
    T Visit(FieldDecl fieldDecl);
    T Visit(LocalVarDecl localVarDecl);
    T Visit(BlockStmt blockStmt);
    T Visit(ExprStmt exprStmt);
    T Visit(VarDeclStmt varDeclStmt);
    T Visit(LoopStmt loopStmt);
    T Visit(IfStmt ifStmt);
    T Visit(ReturnStmt returnStmt);
    T Visit(BreakStmt breakStmt);
    T Visit(ContinueStmt continueStmt);
    T Visit(ConditionExpr conditionExpr);
    T Visit(AssignExpr assignExpr);
    T Visit(ConvertExpr convertExpr);
    T Visit(BinaryExpr binaryExpr);
    T Visit(LogicExpr logicExpr);
    T Visit(UnaryExpr unaryExpr);
    T Visit(PrimaryExpr primaryExpr);
    T Visit(IntegerLiteral integerLiteral);
    T Visit(FloatLiteral floatLiteral);
    T Visit(BooleanLiteral booleanLiteral);
    T Visit(StringLiteral stringLiteral);
    T Visit(NullLiteral nullLiteral);
    T Visit(VariableAccess variableAccess);
    T Visit(FunctionCallExpr expr);
    T Visit(MemberAccessExpr memberAccessExpr);
    T Visit(ObjectCreationExpr expr);
}
    
//完全型つきAST (Highlevel 
public abstract class HirNode
{
    protected HirNode()
    {
    }
    public abstract T Solve<T>(IHirWalker<T> walker);
}
    
//----------------------
// Top Level Declaration
//----------------------
public class ProgramNode : HirNode
{
    public ProgramNode(Ast.ProgramNode ast, ClassDecl[] classes, FunctionDecl[] functions, LocalVarDecl[] variables) : base()
    {
        Ast = ast;
        Classes = classes;
        GlobalFunctions = functions;
        GlobalVariables = variables;
    }
    public Ast.ProgramNode Ast { get; }
    public ClassDecl[] Classes { get; }
    public FunctionDecl[] GlobalFunctions { get; }
    public LocalVarDecl[] GlobalVariables { get; }
    public override T Solve<T>(IHirWalker<T> walker)
    {
        return walker.Visit(this);
    }
}

public class ClassDecl(Ast.ClassDecl ast, int superClassHandle, FunctionDecl[] methods, FieldDecl[] fields)
    : HirNode
{
    public Ast.ClassDecl Ast { get; } = ast;
    public int SuperClassHandle { get; } = superClassHandle;
    public FunctionDecl[] Methods { get; } = methods;
    public FieldDecl[] Fields { get; } = fields;

    public override T Solve<T>(IHirWalker<T> walker)
    {
        return walker.Visit(this);
    }
}

public class FunctionDecl(Ast.FunctionDecl ast, KesType retType, KesType[] argType, BlockStmt body, KesType[] localVariableTypes)
    : HirNode
{
    public Ast.FunctionDecl Ast { get; } = ast;
    public KesType ReturnType { get; } = retType;
    public KesType[] ArgType { get; } = argType;
    public BlockStmt Body { get; } = body;
    public KesType[] LocalVariableTypes { get; } = localVariableTypes;
    public override T Solve<T>(IHirWalker<T> walker) => walker.Visit(this);
}

public class FieldDecl(Ast.VarDecl ast, KesType type) : HirNode
{
    public Ast.VarDecl Ast { get; } = ast;
    public KesType Type { get; } = type;
    public override T Solve<T>(IHirWalker<T> walker) => walker.Visit(this);
}

public class LocalVarDecl(Ast.VarDecl ast, int handle, KesType type, Expr? initializer) : HirNode
{
    public Ast.VarDecl Ast { get; } = ast;
    public int Handle { get; } = handle;
    public KesType Type { get; } = type;
    public Expr? Initializer { get; } = initializer;
    public override T Solve<T>(IHirWalker<T> walker) => walker.Visit(this);
}
    
//------------------------
// 文関連のAST
//------------------------
public abstract class Stmt : HirNode
{
}

public class BlockStmt(Ast.BlockStmt ast, Stmt[] stmts) : Stmt
{
    public Ast.BlockStmt Ast { get; } = ast;
    public Stmt[] Stmts { get; } = stmts;
    public override T Solve<T>(IHirWalker<T> walker) => walker.Visit(this);
}

public class ExprStmt(Ast.ExprStmt ast, Expr expr) : Stmt
{
    public Ast.ExprStmt Ast { get; } = ast;
    public Expr Expr { get; } = expr;

    public override T Solve<T>(IHirWalker<T> walker) => walker.Visit(this);
}

public class VarDeclStmt(Ast.VarDeclStmt ast, LocalVarDecl localVarDecl) : Stmt
{
    public Ast.VarDeclStmt Ast { get; } = ast;
    public LocalVarDecl LocalVarDecl { get; } = localVarDecl;
    public override T Solve<T>(IHirWalker<T> walker) => walker.Visit(this);
}

public class LoopStmt(Ast.Stmt ast, Expr? init, ConditionExpr condition, Expr? update, Stmt body) : Stmt
{
    public Ast.Stmt Ast { get; } = ast;
    public Expr? Init { get; } = init;
    public ConditionExpr Condition { get; } = condition;
    public Expr? Update { get; } = update;
    public Stmt Body { get; } = body;
    public override T Solve<T>(IHirWalker<T> walker) => walker.Visit(this);
}

public class IfStmt(Ast.IfStmt ast, ConditionExpr condition, Stmt thenBody, Stmt elseBody)
    : Stmt
{

    public Ast.IfStmt Ast { get; } = ast;
    public ConditionExpr Condition { get; } = condition;
    public Stmt ThenBody { get; } = thenBody;
    public Stmt ElseBody { get; } = elseBody;

    public override T Solve<T>(IHirWalker<T> walker) => walker.Visit(this);
}

public class ReturnStmt(Ast.ReturnStmt ast, Expr returnExpr) : Stmt
{
    public Ast.ReturnStmt Ast { get; } = ast;
    public Expr ReturnExpr { get; } = returnExpr;
    public override T Solve<T>(IHirWalker<T> walker) => walker.Visit(this);
}

public class BreakStmt(Ast.BreakStmt ast) : Stmt
{
    public Ast.BreakStmt Ast { get; } = ast;
    public override T Solve<T>(IHirWalker<T> walker) => walker.Visit(this);
}

public class ContinueStmt(Ast.ContinueStmt ast) : Stmt
{
    public Ast.ContinueStmt Ast { get; } = ast;
    public override T Solve<T>(IHirWalker<T> walker) => walker.Visit(this);
}

//-------------------------
// 式関連のAST
//-------------------------
public abstract class Expr(KesType retType) : HirNode
{
    public KesType RetType { get; } = retType;
}

public class ConditionExpr(Ast.ConditionExpr ast, Expr expr) : Expr(expr.RetType)
{
    public Ast.ConditionExpr Ast { get; } = ast;
    public Expr Expr { get; } = expr;
    public override T Solve<T>(IHirWalker<T> walker) => walker.Visit(this);
}

public class AssignExpr(Ast.AssignExpr ast, Expr lValue, Expr rValue) : Expr(lValue.RetType)
{
    public Ast.AssignExpr Ast { get; } = ast;
    public Expr LValue { get; } = lValue;
    public Expr RValue { get; } = rValue;
    public override T Solve<T>(IHirWalker<T> walker) => walker.Visit(this);
}

public class ConvertExpr(Expr expr, KesType type) : Expr(type)
{
    public Expr Expr { get; } = expr;
    public override T Solve<T>(IHirWalker<T> walker) => walker.Visit(this);
}

public class BinaryExpr(Ast.BinaryExpr ast, Expr left, Expr right, KesType type) : Expr(type)
{
    public Ast.BinaryExpr Ast { get; } = ast;
    public Expr Left { get; } = left;
    public Expr Right { get; } = right;

    public override T Solve<T>(IHirWalker<T> walker) => walker.Visit(this);
}

public class LogicExpr(Ast.LogicExpr ast, Expr left, Expr right, KesType type) : Expr(type)
{
    public Ast.LogicExpr Ast { get; } = ast;
    public Expr Left { get; } = left;
    public Expr Right { get; } = right;

    public override T Solve<T>(IHirWalker<T> walker) => walker.Visit(this);
}

public class UnaryExpr(Ast.UnaryExpr ast, Expr expr) : Expr(expr.RetType)
{
    public Ast.UnaryExpr Ast { get; } = ast;
    public Expr Expr { get; } = expr;

    public override T Solve<T>(IHirWalker<T> walker) => walker.Visit(this);
}
    
public class PrimaryExpr(Ast.PrimaryExpr ast, Expr expr) : Expr(expr.RetType)
{
    public Ast.PrimaryExpr Ast { get; } = ast;
    public Expr Expr { get; } = expr;

    public override T Solve<T>(IHirWalker<T> walker) => walker.Visit(this);
}

// 終端記号　即値
public class IntegerLiteral(Ast.IntegerLiteral ast) : Expr(KesType.typeOfInt)
{
    public Ast.IntegerLiteral Ast { get; } = ast;

    public override T Solve<T>(IHirWalker<T> walker) => walker.Visit(this);
}
public class FloatLiteral(Ast.FloatLiteral ast) : Expr(KesType.typeOfFloat)
{
    public Ast.FloatLiteral Ast { get; } = ast;
    public override T Solve<T>(IHirWalker<T> walker) => walker.Visit(this);
}
public class BooleanLiteral(Ast.BooleanLiteral ast) : Expr(KesType.typeOfBool)
{
    public Ast.BooleanLiteral Ast { get; } = ast;
    public override T Solve<T>(IHirWalker<T> walker) => walker.Visit(this);
}
public class StringLiteral(Ast.StringLiteral ast) : Expr(KesType.typeOfString)
{
    public Ast.StringLiteral Ast { get; } = ast;
    public override T Solve<T>(IHirWalker<T> walker) => walker.Visit(this);
}
public class NullLiteral(Ast.NullLiteral ast) : Expr(KesType.typeOfNull)
{
    public Ast.NullLiteral Ast { get; } = ast;
    public override T Solve<T>(IHirWalker<T> walker) => walker.Visit(this);
}

// 変数アクセス
public class VariableAccess(Ast.IdentifierName ast, int handle, KesType type, EVarScope scope) : Expr(type)
{
    public Ast.IdentifierName Ast { get; } = ast;
    public EVarScope Scope { get; } = scope;
    public int Handle { get; } = handle;
    public override T Solve<T>(IHirWalker<T> walker) => walker.Visit(this);
}

// 関数呼び出し
public class FunctionCallExpr(Ast.FunctionCallExpr ast, Expr? expr, List<Expr> args, int addr, KesType retType) : Expr(retType)
{
    Ast.FunctionCallExpr Ast { get; } = ast;
    public Expr? Expr { get; } = expr;
    public List<Expr> Args { get; } = args;
    public int Handle { get; } = addr;

    public override T Solve<T>(IHirWalker<T> walker) => walker.Visit(this);
}

public class MemberAccessExpr(Ast.MemberAccessExpr ast, Expr expr, int handle, bool isMethod, KesType retType) : Expr(retType)
{
    public Ast.MemberAccessExpr Ast { get; } = ast;
    public Expr Expr { get; } = expr;
    public bool IsMethod { get; } = isMethod;
    public int Handle { get; } = handle;
    public override T Solve<T>(IHirWalker<T> walker) => walker.Visit(this);
}

public class ObjectCreationExpr(Ast.ObjectCreationExpr ast, KesType type) : Expr(type)
{
    public Ast.ObjectCreationExpr Ast { get; } = ast;
    public override T Solve<T>(IHirWalker<T> walker) => walker.Visit(this);
}