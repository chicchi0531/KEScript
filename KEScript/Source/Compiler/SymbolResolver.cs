using System.Collections;
using KESCompiler.Compiler.Ast;

namespace KESCompiler.Compiler
{
    internal class SemanticsAnalyzeException(KesErrorType error, AstNode node, params object[] optionData) : CompileErrorException(error, optionData)
    {
        public AstNode Node { get; } = node;
    }
    
    class Scope
    {
        struct LocalVariable(
            int level,
            Hir.LocalVarDecl decl
            )
        {
            public readonly int level = level;
            public readonly Hir.LocalVarDecl decl = decl;
        }

        class DictionaryStack<TKey, TValue> : ICollection
        {
            int _index = 0;
            TKey[] _key;
            TValue[] _value;

            public DictionaryStack(int capacity = 32)
            {
                _key = new TKey[capacity];
                _value = new TValue[capacity];
            }
            
            public void Add(TKey key, TValue value)
            {
                if (_index >= _key.Length)
                {
                    Array.Resize(ref _key, _key.Length + 32);
                    Array.Resize(ref _value, _value.Length + 32);
                }
                _key[_index] = key;
                _value[_index] = value;
                _index++;
            }

            public void RollBack(int index)
            {
                _index = index;
            }

            public bool Contains(TKey key)
            {
                return Array.Find(_key, x => x.Equals(key)) != null;
            }
            
            public TValue this[TKey key] => _value[Array.IndexOf(_key, key)];

            public bool TryGetValue(TKey key, out TValue value)
            {
                var index = Array.IndexOf(_key, key);
                if (index == -1)
                {
                    value = default;
                    return false;
                }
                
                value = _value[index];
                return true;
            }

            public bool TryGetValueFromLast(TKey key, out TValue value)
            {
                var index = Array.LastIndexOf(_key, key);
                if (index == -1)
                {
                    value = default;
                    return false;
                }

                value = _value[index];
                return true;
            }

            public void Clear()
            {
                _key = new TKey[32];
                _value = new TValue[32];
                _index = 0;
            }
            
            public IEnumerator GetEnumerator() => _value.GetEnumerator();
            public void CopyTo(Array array, int index) => _value.CopyTo(array, index);
            public int Count => _value.Length;
            public bool IsSynchronized => _value.IsSynchronized;
            public object SyncRoot => _value.SyncRoot;
        }
        
        readonly List<LocalVariable> _variables = new();
        readonly DictionaryStack<string, int> _activeVariableStack = new();

        Stack<int> _baseIndex = new();
        int _currentLevel = 0;

        public Scope()
        {
            _baseIndex.Push(0);
        }
        
        public void BeginScope()
        {
            _currentLevel++;
            _baseIndex.Push(_activeVariableStack.Count);
        }

        public void EndScope()
        {
            _currentLevel--;
            var bp = _baseIndex.Pop();
            _activeVariableStack.RollBack(bp);
        }

        public (int address, Hir.LocalVarDecl? decl) FindVariable(string name)
        {
            // 最後に定義された変数（最も深いところにある変数）から検索する
            if (!_activeVariableStack.TryGetValueFromLast(name, out var index))
            {
                return (-1, null);
            }
            var v = _variables[index];
            return (index, v.decl);
        }
            
        public void DefineVariable(Hir.LocalVarDecl decl)
        {
            if (_activeVariableStack.TryGetValueFromLast(decl.Ast.Name, out var index))
            {
                //異なるスコープ間では同名の変数を許容する（スコープの深いほうを優先する）
                //同じスコープ内で同名の変数を定義するとエラーとする
                var v = _variables[index];
                if (v.level == _currentLevel)
                {
                    throw new SemanticsAnalyzeException(ErrorCode.TypeAlreadyDefined, decl.Ast, decl.Ast.Name);
                }
            }
            index = _variables.Count;
            var variable = new LocalVariable(_currentLevel, decl);
            _variables.Add(variable);
            _activeVariableStack.Add(decl.Ast.Name, index);
        }

        public int GetNextVariableHandle()
        {
            return _variables.Count;
        }
        
        public KesType[] GetVariablesLayout()
        {
            return _variables.Select(x => x.decl.Type).ToArray();
        }
    }
    
    public class SymbolResolver : IAstWalker<Hir.HirNode>
    {
        readonly ILogger _logger;
        readonly Scope _scope = new();
        (int handle, KesElementType? value) _currentClass;
        KesType? _currentScopeReturnType;
        readonly ProgramNode _root;
        readonly KesElementTypeTable _elementTypeTable;
        int _globalVariableCount = 0;

        public SymbolResolver(ILogger logger, KesElementTypeTable typeTable, ProgramNode root)
        {
            _root = root;
            _logger = logger;
            _elementTypeTable = typeTable;
            _currentClass = (-1, null);
            _currentScopeReturnType = null;
        }
        
        public Hir.ProgramNode Resolve()
        {
            return (Hir.ProgramNode)_root.Solve(this);
        }
        
        public Hir.HirNode Visit(AstNode astNode)
        {
            throw new System.NotImplementedException();
        }
        public Hir.HirNode Visit(ProgramNode node)
        {
            List<Hir.ClassDecl> classes = new();
            List<Hir.FunctionDecl> functions = new();
            List<Hir.LocalVarDecl> globalVariables = new();
            try
            {
                foreach (var d in node.Classes)
                {
                    if (d.Solve(this) is Hir.ClassDecl c)
                    {
                        classes.Add(c);
                    }
                }
                foreach (var d in node.GlobalFunctions)
                {
                    if (d.Solve(this) is Hir.FunctionDecl f)
                    {
                        functions.Add(f);
                    }
                }
                foreach (var d in node.GlobalVariables)
                {
                    if (d.Solve(this) is Hir.LocalVarDecl v)
                    {
                        globalVariables.Add(v);
                    }
                }
            }
            catch (SemanticsAnalyzeException e)
            {
                _logger.Error(e.Node.Position, e.ErrorInfo, e.OptionData);
            }
            return new Hir.ProgramNode(node, classes.ToArray(), functions.ToArray(), globalVariables.ToArray());
        }
        public Hir.HirNode Visit(ClassDecl decl)
        {
            // スーパークラスの存在チェック
            int hSuperClass = -1;
            if (decl.SuperClass != null)
            {
                hSuperClass = GetTypeHandle(decl.SuperClass);
                ThrowIfInvalidTypeHandle(hSuperClass, decl.SuperClass);
            }
            
            _currentClass = _elementTypeTable.Find("", decl.Name);
            List<Hir.FieldDecl> fields = new();
            List<Hir.FunctionDecl> methods = new();
            foreach (var d in decl.Variables)
            {
                if (d.Solve(this) is Hir.FunctionDecl f)
                {
                    methods.Add(f);
                }
            }
            foreach (var d in decl.Functions)
            {
                if (d.Solve(this) is Hir.FieldDecl f)
                {
                    fields.Add(f);
                }
            }
            _currentClass = (-1, null);
            return new Hir.ClassDecl(decl, hSuperClass, methods.ToArray(), fields.ToArray());
        }
        
        public Hir.HirNode Visit(FunctionDecl decl)
        {
            // 引数の型解決
            List<KesType> argTypes = new();
            foreach (VarDecl a in decl.Args)
            {
                //引数の型チェック
                if(a.Type is null) throw new ArgumentException();

                int hArgType = GetTypeHandle(a.Type);
                ThrowIfInvalidTypeHandle(hArgType, a.Type);
                var argType = new KesType(hArgType, false, a.IsImmutable);
                
                argTypes.Add(argType);
            }
            
            // 戻り値の型解決
            int hRetElemType = GetTypeHandle(decl.ReturnType);
            ThrowIfInvalidTypeHandle(hRetElemType, decl.ReturnType);
            var retType = new KesType(hRetElemType, false, false);
            
            // 関数本体の解決
            _currentScopeReturnType = retType;
            _scope.BeginScope();
            
            var body = decl.Body.Solve(this) as Hir.BlockStmt ?? throw new ArgumentException();
            var locVarTypeLayouts = _scope.GetVariablesLayout().ToArray();
            
            _scope.EndScope();
            _currentScopeReturnType = null;
            
            return new Hir.FunctionDecl(
                decl, 
                retType, 
                argTypes.ToArray(),
                body,
                locVarTypeLayouts
                );
        }

        public Hir.HirNode Visit(VarDecl decl)
        {
            // クラスフィールドとローカル変数の二通りあるので、ここでは型のみ解決する
            // ローカル変数はVarDeclStmtで定義する
            KesType type;
            switch (decl.VarScope)
            {
                case EVarScope.Global:
                    if (decl.Type is null) throw new ArgumentException(); //グローバル変数は型指定が必須
                    var hElemType = GetTypeHandle(decl.Type);
                    ThrowIfInvalidTypeHandle(hElemType, decl.Type);

                    return new Hir.LocalVarDecl(decl, _globalVariableCount++, new KesType(hElemType, false, decl.IsImmutable), null);

                case EVarScope.Local:
                    // 型推論が必要な場合は、型推論を行う
                    Hir.Expr? initializer = null;
                    if (decl.Type is null)
                    {
                        initializer = (Hir.Expr)decl.InitValue.Solve(this);
                        if (initializer is null) throw new ArgumentException();
                        type = initializer.RetType;
                    }
                    else
                    {
                        hElemType = GetTypeHandle(decl.Type);
                        ThrowIfInvalidTypeHandle(hElemType, decl.Type);
                        type = new KesType(hElemType, false, decl.IsImmutable);
                    }
                    // ローカル変数を定義
                    var locNode = new Hir.LocalVarDecl(decl, _scope.GetNextVariableHandle(), type, initializer);
                    _scope.DefineVariable(locNode);
                    return locNode;

                case EVarScope.ClassField:
                    if (decl.Type is null) throw new ArgumentException(); //クラスフィールドは型指定が必須
                    hElemType = GetTypeHandle(decl.Type);
                    ThrowIfInvalidTypeHandle(hElemType, decl.Type);
                    return new Hir.FieldDecl(decl, new KesType(hElemType, false, decl.IsImmutable));

                case EVarScope.FunctionArg:
                    if (decl.Type is null) throw new ArgumentException(); //関数引数は型指定が必須
                    // 関数引数はローカル変数として定義
                    hElemType = GetTypeHandle(decl.Type);
                    ThrowIfInvalidTypeHandle(hElemType, decl.Type);
                    
                    locNode = new Hir.LocalVarDecl(
                        decl,
                        _scope.GetNextVariableHandle(), 
                        new KesType(hElemType, false, decl.IsImmutable), 
                        null);
                    _scope.DefineVariable(locNode);
                    return locNode;
            }
            throw new ArgumentException();
        }
        public Hir.HirNode Visit(BlockStmt stmt)
        {
            _scope.BeginScope();
            List<Hir.Stmt> stmts = new();
            foreach (var s in stmt.Stmts)
            {
                var result = (Hir.Stmt) s.Solve(this);
                stmts.Add(result);
            }
            _scope.EndScope();
            return new Hir.BlockStmt(stmt, stmts.ToArray());
        }
        public Hir.HirNode Visit(IfStmt stmt)
        {
            var condNode = (Hir.ConditionExpr) stmt.Condition.Solve(this);
            var thenNode = (Hir.Stmt) stmt.ThenBody.Solve(this);
            var elseNode = (Hir.Stmt) stmt.ElseBody.Solve(this);
            return new Hir.IfStmt(stmt, condNode, thenNode, elseNode);
        }
        public Hir.HirNode Visit(WhileStmt stmt)
        {
            var condition = (Hir.ConditionExpr) stmt.Condition.Solve(this);
            var body = (Hir.Stmt) stmt.Body.Solve(this);
            return new Hir.LoopStmt(stmt, null, condition, null, body);
        }
        public Hir.HirNode Visit(ForStmt stmt)
        {
            _scope.BeginScope();
            var init = (Hir.Expr) stmt.Init.Solve(this);
            var cond = (Hir.ConditionExpr) stmt.Condition.Solve(this);
            var update = (Hir.Expr) stmt.Update.Solve(this);
            var body = (Hir.Stmt) stmt.Body.Solve(this);
            _scope.EndScope();
            return new Hir.LoopStmt(stmt, init, cond, update, body);
        }
        public Hir.HirNode Visit(ReturnStmt stmt)
        {
            //自身のスコープの返り値と一致するか確認
            var expr = (Hir.Expr) stmt.ReturnExpr.Solve(this);
            if(_currentScopeReturnType != expr.RetType)
            {
                throw new SemanticsAnalyzeException(ErrorCode.InvalidTypeOfReturn, stmt);
            }
            return new Hir.ReturnStmt(stmt, expr);
        }
        public Hir.HirNode Visit(BreakStmt stmt)
        {
            return new Hir.BreakStmt(stmt);
        }
        public Hir.HirNode Visit(ContinueStmt stmt)
        {
            return new Hir.ContinueStmt(stmt);
        }
        public Hir.HirNode Visit(VarDeclStmt stmt)
        {
            var decl = (Hir.LocalVarDecl) stmt.VarDecl.Solve(this);
            return new Hir.VarDeclStmt(stmt, decl);
        }
        public Hir.HirNode Visit(ExprStmt stmt)
        {
            var expr = (Hir.Expr) stmt.Expr.Solve(this);
            return new Hir.ExprStmt(stmt, expr);
        }

        //----------------
        // Expr
        //----------------
        public Hir.HirNode Visit(ConditionExpr expr)
        {
            var node = expr.Expr.Solve(this) as Hir.Expr;
            if(node is null) throw new ArgumentException();

            var decl = _elementTypeTable.Get(node.RetType.ElementTypeHandle);
            if (node.RetType is not { ElementTypeHandle: KesElementTypeTable.boolTypeHandle, IsArray: false })
            {
                throw new SemanticsAnalyzeException(ErrorCode.InvalidTypeInCondition, expr);
            }
            
            return new Hir.ConditionExpr(expr, node);
        }

        public Hir.HirNode Visit(AssignExpr expr)
        {
            var lvalueNode = expr.LValue.Solve(this) as Hir.Expr;
            var rvalueNode = expr.RValue.Solve(this) as Hir.Expr;
            if (lvalueNode is null || rvalueNode is null) throw new ArgumentException();
            
            // lvalueが左辺値であることは構文解析時に保証されている
            var lvalueType = lvalueNode.RetType;
            var rvalueType = rvalueNode.RetType;
            var lvalueTypeElem = _elementTypeTable.Get(lvalueType.ElementTypeHandle) ?? throw new ArgumentException();
            var rvalueTypeElem = _elementTypeTable.Get(rvalueType.ElementTypeHandle) ?? throw new ArgumentException();
            
            // NG: immutableな変数への代入
            if (lvalueType.IsImmutable)
            {
                throw new SemanticsAnalyzeException(ErrorCode.CannotAssignToImmutableVariable, expr);
            }
            
            // OK: 同じ型への代入
            if (lvalueType == rvalueType)
            {
                return new Hir.AssignExpr(expr, lvalueNode, rvalueNode);
            }
            
            // NG: 同じ型ではない配列同士の代入は禁止
            if (lvalueType.IsArray || rvalueType.IsArray)
            {
                throw new SemanticsAnalyzeException(ErrorCode.CannotConvertType, expr, rvalueTypeElem.Name, lvalueTypeElem.Name);
            }
            
            // OK: decimal <- int
            if (lvalueType == KesType.typeOfFloat && rvalueType == KesType.typeOfInt)
            {
                // decimal型への変換ノードを挿入
                var convNode = new Hir.ConvertExpr(rvalueNode, KesType.typeOfFloat);
                return new Hir.AssignExpr(expr, lvalueNode, convNode);
            }
            
            // OK: 参照型へのnull代入
            if (lvalueTypeElem.IsReference && rvalueType == KesType.typeOfNull)
            {
                return new Hir.AssignExpr(expr, lvalueNode, rvalueNode);
            }
            
            // OK: class <- class.super
            var superClassList = _elementTypeTable.GetSuperClassList(lvalueType.ElementTypeHandle,
                () => { throw new SemanticsAnalyzeException(ErrorCode.CircularInheritance, expr, lvalueTypeElem.Name); });
            if (superClassList.Contains(rvalueType.ElementTypeHandle))
            {
                return new Hir.AssignExpr(expr, lvalueNode, rvalueNode);
            }
            
            throw new SemanticsAnalyzeException(ErrorCode.CannotConvertType, expr, rvalueTypeElem.Name, lvalueTypeElem.Name);
        }
        public Hir.HirNode Visit(BinaryExpr expr)
        {
            Hir.BinaryExpr? result;
            var op = expr.Op;
            var lvalue = expr.Left.Solve(this) as Hir.Expr;
            var rvalue = expr.Right.Solve(this) as Hir.Expr;
            var lType = lvalue.RetType;
            var rType = rvalue.RetType;
            var lTypeElem = _elementTypeTable.Get(lType.ElementTypeHandle) ?? throw new ArgumentException();
            var rTypeElem = _elementTypeTable.Get(rType.ElementTypeHandle) ?? throw new ArgumentException();
            var lTypename = KesTypeUtility.GetTypeName(lType, _elementTypeTable);
            var rTypename = KesTypeUtility.GetTypeName(rType, _elementTypeTable);
            
            // 配列型で型が違う場合はNG
            if (lType == rType && lType.IsArray || rType.IsArray)
            {
                throw new SemanticsAnalyzeException(ErrorCode.CannotConvertType, expr, lTypename, rTypename);
            }
            
            // 異なる型同士の暗黙的変換処理
            {
                // int -> floatへの暗黙的型変換
                if (lType == KesType.typeOfFloat && rType == KesType.typeOfInt)
                {
                    var conv = new Hir.ConvertExpr(rvalue, lType);
                    rvalue = conv;
                    rType = lType;
                }
                else if (lType == KesType.typeOfInt && rType == KesType.typeOfFloat)
                {
                    var conv = new Hir.ConvertExpr(lvalue, rType);
                    lvalue = conv;
                    lType = rType;
                }
                
                // object <op> null の処理
                else if (lType == KesType.typeOfNull && !rTypeElem.IsReference)
                {
                    lType = rType;
                }
                else if (lTypeElem.IsReference && rType == KesType.typeOfNull)
                {
                    rType = lType;
                }
            }

            //この時点で型が一致していない場合はエラーとする。
            if (lType != rType)
            {
                throw new SemanticsAnalyzeException(ErrorCode.InvalidTypeOfOperatorBinary, expr, op, lTypename, rTypename);
            }
            result = new Hir.BinaryExpr(expr, lvalue, rvalue, lType);
            
            // 型チェック
            switch (op)
            {
                case KesOperator.Add:
                case KesOperator.Sub:
                case KesOperator.Mul:
                case KesOperator.Div:
                case KesOperator.Mod:
                case KesOperator.Or:
                case KesOperator.And:
                    if (!CheckCalcOperator(lType, op)) throw new SemanticsAnalyzeException(ErrorCode.InvalidTypeOfOperator, expr, op, lTypename);
                    break;
                case KesOperator.Eq:
                case KesOperator.NotEq:
                    //型が一致していれば、=と!=は型を問わないため、チェック不要
                    break;
                case KesOperator.Lt:
                case KesOperator.Gt:
                case KesOperator.Le:
                case KesOperator.Ge:
                    if (!CheckCompareOperator(lType)) throw new SemanticsAnalyzeException(ErrorCode.InvalidTypeOfOperator, expr, op, lTypename);
                    break;
                default:
                    //ここへ到達した場合は、誤った演算子をパース時に読み込んでいる可能性がある
                    throw new ArgumentException(op.ToString());
            }
            
            return result;
        }
        public Hir.HirNode Visit(LogicExpr expr)
        {
            var lvalue = (Hir.Expr)expr.Left.Solve(this);
            var rvalue = (Hir.Expr)expr.Right.Solve(this);
            var lType = lvalue.RetType;
            var rType = rvalue.RetType;
            var lTypename = KesTypeUtility.GetTypeName(lType, _elementTypeTable);
            var rTypename = KesTypeUtility.GetTypeName(rType, _elementTypeTable);
            
            if (lvalue != rvalue)
            {
                throw new SemanticsAnalyzeException(ErrorCode.InvalidTypeOfOperatorBinary, expr, expr.Op, lTypename, rTypename);
            }

            // 型チェック
            switch (expr.Op)
            {
                case KesOperator.LogAnd:
                case KesOperator.LogOr:
                    if(!CheckLogicalOperator(lType)) throw new SemanticsAnalyzeException(ErrorCode.InvalidTypeOfOperator, expr, expr.Op, lTypename);
                    break;
                default:
                    //ここへ到達した場合は、誤った演算子をパース時に読み込んでいる可能性がある
                    throw new ArgumentException(expr.Op.ToString());
            }

            return new Hir.LogicExpr(expr, lvalue, rvalue, lType);
        }
        public Hir.HirNode Visit(UnaryExpr expr)
        {
            var op = expr.Op;
            var lvalue = (Hir.Expr)expr.Expr.Solve(this);
            var lType = lvalue.RetType;
            var lTypename = KesTypeUtility.GetTypeName(lType, _elementTypeTable);

            bool checkResult = false;
            switch (expr.Op)
            {
                case KesOperator.Minus: checkResult = CheckMinusOperator(lType); break;
                case KesOperator.Not: checkResult = CheckNotOperator(lType); break;
                case KesOperator.PreIncr: checkResult = CheckIncrementOperator(lType); break;
                case KesOperator.PreDecr: checkResult = CheckIncrementOperator(lType); break;
                default:
                    //ここへ到達した場合は、誤った演算子をパース時に読み込んでいる可能性がある
                    throw new ArgumentException(op.ToString());
            }
            if (!checkResult) throw new SemanticsAnalyzeException(ErrorCode.InvalidTypeOfOperator, expr, op, lTypename);

            return new Hir.UnaryExpr(expr, lvalue);
        }
        public Hir.HirNode Visit(PrimaryExpr expr)
        {
            var op = expr.Op;
            var lvalue = (Hir.Expr)expr.Expr.Solve(this);
            var lType = lvalue.RetType;
            var lTypename = KesTypeUtility.GetTypeName(lType, _elementTypeTable);
            
            switch (op)
            {
                case KesOperator.PostIncr:
                case KesOperator.PostDecr:
                    if(!CheckIncrementOperator(lType)) throw new SemanticsAnalyzeException(ErrorCode.InvalidTypeOfOperator, expr, op, lTypename);
                    break;
                default: 
                    //ここへ到達した場合は、誤った演算子をパース時に読み込んでいる可能性がある
                    throw new ArgumentException(op.ToString());
            }
            
            return new Hir.PrimaryExpr(expr, lvalue);
        }
        public Hir.HirNode Visit(IntegerLiteral integerLiteral)
        {
            return new Hir.IntegerLiteral(integerLiteral);
        }
        public Hir.HirNode Visit(FloatLiteral floatLiteral)
        {
            return new Hir.FloatLiteral(floatLiteral);
        }
        public Hir.HirNode Visit(BooleanLiteral booleanLiteral)
        {
            return new Hir.BooleanLiteral(booleanLiteral);
        }
        public Hir.HirNode Visit(StringLiteral stringLiteral)
        {
            return new Hir.StringLiteral(stringLiteral);
        }
        public Hir.HirNode Visit(NullLiteral nullLiteral)
        {
            return new Hir.NullLiteral(nullLiteral);
        }
        public Hir.HirNode Visit(PredefinedTypeName predefinedTypeName)
        {
            throw new NotImplementedException();
        }
        public Hir.HirNode Visit(IdentifierName identifierName)
        {
            var name = identifierName.Name;
            
            //ローカル変数から検索
            var v = _scope.FindVariable(name);
            if (v.address != -1)
            {
                return new Hir.VariableAccess(identifierName, v.address, v.decl.Type, EVarScope.Local);
            }
            
            //メンバフィールドから検索
            if (_currentClass.value != null)
            {
                var field = _currentClass.value.FieldTable
                    .Select((value, handle) => (handle,value))
                    .FirstOrDefault(x => x.value.Name == name);
                if (field.handle != -1)
                {
                    return new Hir.VariableAccess(identifierName, field.handle, field.value.Type, EVarScope.ClassField);
                }
            }
            
            //グローバル変数から検索
            var gb = _root.GlobalVariables
                .Select((value, index) => (index, value))
                .FirstOrDefault(x => x.value.Name == name);
            if (gb.value is null)
            {
                throw new SemanticsAnalyzeException(ErrorCode.UsedUndefinedSymbol, identifierName, name);
            }
            
            var type = _elementTypeTable.Find(gb.value.Type);
            if (type.value is null)
            {
                throw new SemanticsAnalyzeException(ErrorCode.TypeNotFound, identifierName, gb.value.Type);
            }

            return new Hir.VariableAccess(identifierName, gb.index, new KesType(type.handle, false, gb.value.IsImmutable), EVarScope.Global);
        }
        public Hir.HirNode Visit(ModuleName moduleName)
        {
            throw new NotImplementedException();
        }

        public Hir.HirNode Visit(FunctionCallExpr functionCallExpr)
        {
            Hir.Expr? parentExpr;
            int handle = 0;
            KesType retType;
            
            var expr = functionCallExpr.Expr;
            //メンバメソッド、グローバル関数コール
            if (expr is IdentifierName i)
            {
                var funcName = i.Name;
                
                //メンバメソッドから検索
                var method = _currentClass.value?.MethodTable
                    .Select((value, index) => (index,value))
                    .FirstOrDefault(x => x.value.Name == funcName);

                if (method != null)
                {
                    handle = method.Value.index;
                    retType = method.Value.value.ReturnType;
                }
                else
                {
                    //グローバル関数から検索
                    var func = _root.GlobalFunctions
                        .Select((value, index) => (index, value))
                        .FirstOrDefault(x => x.value.Name == funcName);
                    var retElem = _elementTypeTable.Find(func.value.ReturnType);
                    if (retElem.value is null)
                    {
                        throw new SemanticsAnalyzeException(ErrorCode.InvalidTypeOfReturn, functionCallExpr, funcName);
                    }
                    handle = func.index;
                    retType = new KesType(retElem.handle,false, false);
                }
                
                parentExpr = null;
            }
            //自身以外のメソッドコール
            else if(expr is MemberAccessExpr)
            {
                Hir.MemberAccessExpr maExpr = expr.Solve(this) as Hir.MemberAccessExpr ?? throw new ArgumentException();
                if(!maExpr.IsMethod) throw new SemanticsAnalyzeException(ErrorCode.FieldUsedAsMethod, functionCallExpr);

                parentExpr = maExpr.Expr;
                handle = maExpr.Handle;
                retType = maExpr.RetType;
            }
            else
            {
                throw new NotImplementedException();
            }
            
            //todo 引数のレイアウトチェック
            
            //引数の型チェック
            List<Hir.Expr> args = new();
            foreach (var arg in functionCallExpr.Args)
            {
                args.Add(arg.Solve(this) as Hir.Expr ?? throw new ArgumentException());
            }
            return new Hir.FunctionCallExpr(functionCallExpr, parentExpr, args, handle, retType);
        }
        
        public Hir.HirNode Visit(MemberAccessExpr memberAccessExpr)
        {
            var expr = memberAccessExpr.Expr.Solve(this) as Hir.Expr;
            if (expr is null) throw new SemanticsAnalyzeException(ErrorCode.InvalidMemberAccessFormat, memberAccessExpr);
            var prevType = _elementTypeTable.Get(expr.RetType.ElementTypeHandle);

            //メンバーを、フィールドから探す
            var field = prevType.FieldTable
                .Select((value,index) => (value,index))
                .FirstOrDefault(x => x.value.Name == memberAccessExpr.Name);
            if (field.value != null)
            {
                return new Hir.MemberAccessExpr(memberAccessExpr, expr, field.index, false, field.value.Type);
            }
            
            //メソッドから探す
            var method = prevType.MethodTable
                .Select((value, index) => (value, index))
                .FirstOrDefault(x => x.value.Name == memberAccessExpr.Name);
            if(method.value != null)
                return new Hir.MemberAccessExpr(memberAccessExpr, expr, method.index, true, method.value.ReturnType);
            
            throw new SemanticsAnalyzeException(ErrorCode.MemberNotFound, memberAccessExpr, prevType.Name, memberAccessExpr.Name);
        }
        
        public Hir.HirNode Visit(ObjectCreationExpr objectCreationExpr)
        {
            var (hType, type) = _elementTypeTable.Find(objectCreationExpr.TypeName);
            ThrowIfInvalidTypeHandle(hType, objectCreationExpr);
            
            //インスタンス生成できるのはリファレンス型のみ
            if (!type.IsReference)
            {
                throw new SemanticsAnalyzeException(ErrorCode.CannotCreateObjectAsPrimitiveType, objectCreationExpr, objectCreationExpr.TypeName);
            }
            
            //memo: 配列の場合ArrayCreateExprが呼ばれるため、このノードが呼ばれる場合は配列ではないことが確定している
            return new Hir.ObjectCreationExpr(objectCreationExpr, new KesType(hType,false, false));
        }

        // 指定した型名ツリーから型のハンドルを取得
        int GetTypeHandle(Expr? expr) => expr is null ? -1 : _elementTypeTable.Find(expr).handle;
        static void ThrowIfInvalidTypeHandle(int address, Expr typeNameExpr)
        {
            if (address == -1) throw new SemanticsAnalyzeException(ErrorCode.TypeNotFound, typeNameExpr, typeNameExpr);
        }
        
        // +-*/%&|の演算子型チェック
        bool CheckCalcOperator(KesType t, KesOperator op)
        {
            if (t.IsArray) return false;
            if (t.ElementTypeHandle is KesElementTypeTable.intTypeHandle or KesElementTypeTable.floatTypeHandle) return true;
            if(op == KesOperator.Add && t.ElementTypeHandle == KesElementTypeTable.stringTypeHandle) return true;
            
            return false;
        }
        
        // <, >, <=, >=の演算子型チェック
        bool CheckCompareOperator(KesType t)
        {
            if (t.IsArray) return false;
            if(t.ElementTypeHandle is KesElementTypeTable.intTypeHandle or KesElementTypeTable.floatTypeHandle) return true;
            
            return false;
        }
        
        // &&, ||の演算子型チェック
        bool CheckLogicalOperator(KesType t)
        {
            if(t.IsArray) return false;
            if(t.ElementTypeHandle == KesElementTypeTable.boolTypeHandle) return true;
            return false;
        }
        
        // ++ --の演算子型チェック
        bool CheckIncrementOperator(KesType t)
        {
            if(t.IsArray) return false;
            if (t.ElementTypeHandle == KesElementTypeTable.intTypeHandle) return true;
            
            return false;
        }
        
        // -の演算子型チェック
        bool CheckMinusOperator(KesType t)
        {
            if(t.IsArray) return false;
            if (t.ElementTypeHandle is KesElementTypeTable.intTypeHandle or KesElementTypeTable.floatTypeHandle) return true;
            
            return false;
        }
        
        // !の演算子チェック
        bool CheckNotOperator(KesType t)
        {
            if (t.IsArray) return false;
            if (t.ElementTypeHandle == KesElementTypeTable.boolTypeHandle) return true;

            return false;
        }
        
        
    }
}
