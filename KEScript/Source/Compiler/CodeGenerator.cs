using KESCompiler.Compiler.Hir;
using KESCompiler.Runtime;
using ValueType = KESCompiler.Runtime.ValueType;

namespace KESCompiler.Compiler;

public class CodeGenerator(ILogger logger, ProgramNode root) : IHirWalker<object?>
{
    class CodeGeneratorException(KesErrorType error, CodePosition pos, params object[] optionData) : CompileErrorException(error, optionData)
    {
        public CodePosition Position { get; } = pos;
    }

    struct Variable(string name, string typename, int address, int size)
    {
        public string name = name;
        public string typename = typename;
        public int address = address;
        public int size = size;
    }

    List<Operation> _program = new();
    List<string> _stringBuffer = new();
    List<ClassData> _classTable = new();
    int _entryPointAddress = 0;

    public ProgramData Generate()
    {
        root.Solve(this);
            
        return new ProgramData()
        {
            Signature = "KES",
            MajorVersion = 1,
            MinorVersion = 0,
            EntryPoint = _entryPointAddress,
            Program = _program.ToArray(),
            StringBuffer = _stringBuffer.ToArray(),
            ClassTable = _classTable.ToArray()
        };
    }
        
    public object? Visit(HirNode astNode) => throw new System.NotImplementedException();
    public object? Visit(ProgramNode program)
    {
        root = program;

        try
        {
            foreach (var d in program.GlobalVariables)
            {
                d.Solve(this);
            }
            foreach (var d in program.GlobalFunctions)
            {
                d.Solve(this);
            }
            //グローバル空間のテーブルに格納
            
            foreach (var d in program.Classes)
            {
                d.Solve(this);
            }

            // エントリーポイントがない場合はエラー
            if (_entryPointAddress == 0)
            {
                throw new CodeGeneratorException(ErrorCode.CannotFindEntryPoint, default);
            }
        }
        catch (CodeGeneratorException e)
        {
            logger.Error(e.Position, e.ErrorInfo, e.OptionData);
        }
        return null;
    }
    public object? Visit(ClassDecl decl)
    {
        // メソッドの解析
        List<MethodData> methods = [];
        foreach (var f in decl.Methods)
        {
            if (f.Solve(this) is not MethodData method)
            {
                throw new ArgumentException();
            }
            methods.Add(method);
        }
            
        // フィールドの解析
        List<FieldData> fields = [];
        foreach (var v in decl.Fields)
        {
            if (v.Solve(this) is not FieldData field)
            {
                throw new ArgumentException();
            }
            fields.Add(field);
        }
            
        _classTable.Add(new ClassData(
            decl.SuperClassHandle,
            methods.ToArray(),
            fields.ToArray()
        ));
        return null;
    }
    public object? Visit(FunctionDecl decl)
    {
        var pc = _program.Count;
        
        decl.Body.Solve(this);
        
        return new MethodData(pc, decl.LocalVariableTypeHandles.Length);
    }
    public object? Visit(FieldDecl decl)
    {
        var valueType = TypeHandleToValueType(decl.TypeHandle);
        return new FieldData(valueType);
    }
    public object? Visit(LocalVarDecl decl)
    {
        // 初期化子がある場合は、初期化コードを生成
        if (decl.Initializer is null) return null;
        
        decl.Initializer.Solve(this);
        AddCode(EOpCode.StLoc, decl.Handle);
        return null;
    }
    
    //----------------
    // Statements
    //----------------

    public object? Visit(BlockStmt stmt)
    {
        foreach(var s in stmt.Stmts)
        {
            s.Solve(this);
        }
        return null;
    }
    
    public object? Visit(IfStmt stmt)
    {
        //条件式の解析
        stmt.Condition.Solve(this);
        var ifBeginLabel = AddCode(Runtime.EOpCode.Nop);

        stmt.ThenBody.Solve(this);
        var endThenLabel = AddCode(Runtime.EOpCode.Nop);

        stmt.ElseBody?.Solve(this);
        
        //条件式のジャンプ先を設定
        _program[ifBeginLabel] = new Operation(EOpCode.BrFalse, _program.Count);
        _program[endThenLabel] = new Operation(EOpCode.Br, _program.Count);
        
        return null;
    }
    public object? Visit(LoopStmt stmt)
    {
        var condBeginLabel = _program.Count - 1;
        stmt.Condition.Solve(this);
        var bodyBeginLabel = AddCode(Runtime.EOpCode.Nop);

        stmt.Body.Solve(this);
        
        //条件式のジャンプ先を設定
        var endLabel = _program.Count - 1;
        _program[bodyBeginLabel] = new Operation(EOpCode.BrFalse, endLabel);
        
        //ループ中のbreak,continue命令のジャンプ先を指定
        var loopBody = _program.GetRange(bodyBeginLabel,endLabel);
        for(var i=0; i<loopBody.Count; i++)
        {
            var op = loopBody[i];
            if (op.eOpCode is EOpCode.DummyBreak)
            {
                loopBody[i] = new Operation(EOpCode.Br, endLabel);
            }
            else if (op.eOpCode is EOpCode.DummyContinue)
            {
                loopBody[i] = new Operation(EOpCode.Br, condBeginLabel);
            }
        }
        return null;
    }
    public object? Visit(ReturnStmt stmt)
    {
        stmt.ReturnExpr.Solve(this);
        AddCode(EOpCode.Ret);
        return null;
    }
    public object? Visit(BreakStmt stmt)
    {
        AddCode(EOpCode.DummyBreak);
        return null;
    }
    public object? Visit(ContinueStmt stmt)
    {
        AddCode(EOpCode.DummyContinue);
        return null;
    }
    public object? Visit(VarDeclStmt stmt)
    {
        stmt.LocalVarDecl.Solve(this);
        return null;
    }
    public object? Visit(ExprStmt stmt)
    {
        stmt.Expr.Solve(this);
        AddCode(EOpCode.Pop);   //exprはすべて返り値を持つため、最後に残った返り値は捨てる
        return null;
    }
    
    //----------------
    // Expressions
    //----------------
    public object? Visit(ConditionExpr expr)
    {
        expr.Expr.Solve(this);
        return null;
    }
    public object? Visit(AssignExpr expr)
    {
        var target = expr.LValue;
        if (target is MemberAccessExpr m)
        {
            m.Expr.Solve(this);
            expr.RValue.Solve(this);
            AddCode(EOpCode.Dup); //rvalueを複製（exprの返り値のため）
            AddCode(EOpCode.StFld, m.Handle); //stfld
        }
        else if(target is IdentifierNode i)
        {
            expr.RValue.Solve(this);
            AddCode(EOpCode.Dup); //rvalueを複製（exprの返り値のため）
            i.Solve(this); //stloc or stglb
        }
        else
        {
            throw new NotImplementedException();
        }
        return null;
    }
    public object? Visit(ConvertExpr convertExpr)
    {
        convertExpr.Expr.Solve(this);
        switch (TypeHandleToValueType(convertExpr.TypeHandle))
        {
            case ValueType.Int32: AddCode(EOpCode.ConvI4); break;
            case ValueType.Float32: AddCode(EOpCode.ConvR4); break;
        }
        return null;
    }
    public object? Visit(BinaryExpr expr)
    {
        expr.Left.Solve(this);
        expr.Right.Solve(this);

        var op = expr.Ast.Op switch
        {
            Ast.KesOperator.Add => EOpCode.Add,
            Ast.KesOperator.Sub => EOpCode.Sub,
            Ast.KesOperator.Mul => EOpCode.Mul,
            Ast.KesOperator.Div => EOpCode.Div,
            Ast.KesOperator.Mod => EOpCode.Mod,
            Ast.KesOperator.Eq => EOpCode.Ceq,
            Ast.KesOperator.NotEq => EOpCode.Ceq,
            Ast.KesOperator.Le => EOpCode.Cle,
            Ast.KesOperator.Lt => EOpCode.Clt,
            Ast.KesOperator.Ge => EOpCode.Cge,
            Ast.KesOperator.Gt => EOpCode.Cgt,
            Ast.KesOperator.LogAnd => EOpCode.And,
            Ast.KesOperator.LogOr => EOpCode.Or,
            _ => throw new NotImplementedException()
        };
        AddCode(op);
        
        return null;
    }
    public object? Visit(LogicExpr expr)
    {
        expr.Left.Solve(this);
        expr.Right.Solve(this);

        var op = expr.Ast.Op switch
        {
            Ast.KesOperator.LogAnd => EOpCode.And,
            Ast.KesOperator.LogOr => EOpCode.Or,
            _ => throw new NotImplementedException()
        };
        AddCode(op);
        
        return null;
    }
    public object? Visit(UnaryExpr expr)
    {
        //右辺を先に解決
        expr.Expr.Solve(this);

        var op = expr.Ast.Op switch
        {
            Ast.KesOperator.Minus => EOpCode.Neg,
            Ast.KesOperator.Not => EOpCode.Not,
            Ast.KesOperator.PreIncr => EOpCode.Inc,
            Ast.KesOperator.PreDecr => EOpCode.Dec,
            _ => throw new NotImplementedException()
        };
        AddCode(op);
        return null;
    }
    public object? Visit(PrimaryExpr expr)
    {
        expr.Expr.Solve(this);

        var op = expr.Ast.Op switch
        {
            Ast.KesOperator.PostIncr => EOpCode.Inc,
            Ast.KesOperator.PostDecr => EOpCode.Dec,
            _ => throw new NotImplementedException()
        };
        AddCode(op);
        return null;
    }
    public object? Visit(IntegerLiteral integerLiteral)
    { 
        AddCode(EOpCode.LdcI4, integerLiteral.Ast.Value);
        return null;
    }
    public object? Visit(FloatLiteral floatLiteral)
    { 
        AddCode(EOpCode.LdcR4, floatLiteral.Ast.Value);
        return null;
    }
    public object? Visit(BooleanLiteral booleanLiteral)
    {
        AddCode(EOpCode.LdcI1, (byte)(booleanLiteral.Ast.Value ? 1 : 0));
        return null;
    }
    public object? Visit(StringLiteral stringLiteral)
    {
        _stringBuffer.Add(stringLiteral.Ast.Value);
        AddCode(EOpCode.LdStr, _stringBuffer.Count - 1);
        return null;
    }
    public object? Visit(NullLiteral nullLiteral)
    {
        AddCode(EOpCode.LdNull);
        return null;
    }
    public object? Visit(IdentifierNode identifierNode) => throw new NotImplementedException();
    public object? Visit(LoadLocalVariable expr)
    {
        AddCode(EOpCode.LdLoc, expr.Handle);
        return null;
    }
    public object? Visit(LoadGlobalVariable expr)
    {
        AddCode(EOpCode.LdGlb, expr.Handle);
        return null;
    }
    public object? Visit(StoreLocalVariable expr)
    { 
        AddCode(EOpCode.StLoc, expr.Handle);
        return null;
    }
    public object? Visit(StoreGlobalVariable expr)
    {
        AddCode(EOpCode.StGlb, expr.Handle);
        return null;
    }

    public object? Visit(FunctionCallExpr expr)
    {
        //引数を積む（-1,-2,-3,...のようにアクセスするので逆順に積む）
        for(int i=expr.Args.Count-1; i>=0; i--)
        {
            expr.Args[i].Solve(this);
        }
        
        //コール（bpをスタックに積み、pcを書き換える。ローカル変数分のスタック確保）
        AddCode(EOpCode.Call, expr.Address);
        
        return null;
    }
    public object? Visit(MemberAccessExpr expr)
    {
        expr.Expr.Solve(this);
        AddCode(EOpCode.LdFld, expr.Handle);
        return null;
    }
    public object? Visit(ObjectCreationExpr expr)
    {
        AddCode(EOpCode.NewObj, expr.TypeHandle);
        return null;
    }

    int AddCode(EOpCode code)
    {
        _program.Add(new Operation(code));
        return _program.Count - 1;
    }
    int AddCode(EOpCode code, int operand)
    {
        _program.Add(new Operation(code, operand));
        return _program.Count - 1;
    }
    int AddCode(EOpCode code, float operand)
    {
        _program.Add(new Operation(code, operand));
        return _program.Count - 1;
    }
    int AddCode(EOpCode code, bool operand)
    {
        _program.Add(new Operation(code, operand));
        return _program.Count - 1;
    }
    int AddCode(EOpCode code, string value)
    {
        var index = _stringBuffer.IndexOf(value);
        if (index == -1)
        {
            index = _stringBuffer.Count;
            _stringBuffer.Add(value);
        }
        var address = new Address(index, 0);
        _program.Add(new Operation(code, address));
        return _program.Count - 1;
    }
    
    static Runtime.ValueType TypeHandleToValueType(int handle) => handle switch
    {
        0 => Runtime.ValueType.Int32,
        1 => Runtime.ValueType.Float32,
        2 => Runtime.ValueType.Boolean,
        _ => Runtime.ValueType.Address
    };
}