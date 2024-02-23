using KESCompiler.Compiler.Ast;

namespace KESCompiler.Compiler
{
    internal class SemanticsAnalyzeException(KesErrorType error, CodePosition pos, params object[] optionData) : CompileErrorException(error, optionData)
    {
        public CodePosition Position { get; } = pos;
    }
    
    public readonly struct TypeInfo(Expr fullQualifiedName, bool isImmutable)
    {
        public readonly Expr fullQualifiedName = fullQualifiedName;
        public readonly bool isImmutable = isImmutable;

        public override bool Equals(object? obj)
        {
            if (obj is TypeInfo info)
            {
                return fullQualifiedName == info.fullQualifiedName;
            }
            return false;
        }

        public override int GetHashCode() => (typename: fullQualifiedName, isImmutable).GetHashCode();

        public static bool operator ==(TypeInfo left, TypeInfo right) => left.Equals(right);
        public static bool operator !=(TypeInfo left, TypeInfo right) => !(left == right);

    }
    
    class Scope
    {
        public Scope(Scope parent)
        {
            Parent = parent;
        }
        readonly List<VarDecl> _variables = new();
        public List<Scope> Children { get; } = new();
        public Scope Parent { get; } = null;

        public VarDecl FindVariable(string name)
        {
            var v = _variables.Find(x => x.Name == name);
            return v ?? Parent?.FindVariable(name);
        }
            
        public void DefineVariable(VarDecl decl)
        {
            _variables.Add(decl);
        }
    }
    
    public class SymbolResolver : IAstWalker<Hir.HirNode>
    {
        readonly ILogger _logger;
        Scope _currentScope;
        ClassDecl _currentAstClass;
        Expr _currentScopeReturnType;
        ProgramNode _root;

        public SymbolResolver(ILogger logger)
        {
            _logger = logger;
        }
        
        public Hir.HirNode Resolve(ProgramNode root)
        {
            _root = root;
            return _root.Solve(this);
        }
        
        public Hir.HirNode Visit(AstNode astNode)
        {
            throw new System.NotImplementedException();
        }
        public Hir.HirNode Visit(ProgramNode node)
        {
            try
            {
                List<Hir.ClassDecl> classes = new();
                foreach (var d in node.Classes)
                {
                    if (node.Solve(this) is Hir.ClassDecl c)
                    {
                        classes.Add(c);
                    }
                }
                
                List<Hir.FunctionDecl> functions = new();
                foreach (var d in node.GlobalFunctions)
                {
                    d.Solve(this);
                }
                
                List<Hir.FieldDecl> globalVariables = new();
                foreach (var d in node.GlobalVariables)
                {
                    d.Solve(this);
                }
            }
            catch (SemanticsAnalyzeException e)
            {
                _logger.Error(e.Position, e.ErrorInfo, e.OptionData);
            }
            return default;
        }
        public TypeInfo Visit(ClassDecl decl)
        {
            _currentAstClass = decl;
            foreach (var d in decl.Variables)
            {
                d.Solve(this);
            }
            foreach (var d in decl.Functions)
            {
                d.Solve(this);
            }
            _currentAstClass = null;
            return default;
        }
        
        public TypeInfo Visit(FunctionDecl decl)
        {
            // 引数の型解決
            foreach (var a in decl.Args)
            {
                //引数の型チェック
                CheckTypeExist(a.Type, decl.Position);
            }
            
            // 戻り値の型解決
            CheckTypeExist(decl.ReturnType, decl.Position);
            _currentScopeReturnType = decl.ReturnType;
            _currentScope = null;
            decl.Body.Solve(this);
            _currentScopeReturnType = null;
            
            return default;
        }
        public TypeInfo Visit(VarDecl decl)
        {
            // クラスフィールドとローカル変数の二通りあるので、ここでは型のみ解決する
            // ローカル変数はVarDeclStmtで定義する
            CheckTypeExist(decl.Type, decl.Position);
            return default;
        }
        public TypeInfo Visit(BlockStmt stmt)
        {
            NewScope();
            foreach (var s in stmt.Stmts)
            {
                s.Solve(this);
            }
            EndScope();
            return default;
        }
        public TypeInfo Visit(IfStmt stmt)
        {
            stmt.Condition.Solve(this);
            stmt.ThenBody.Solve(this);
            stmt.ElseBody.Solve(this);
            return default;
        }
        public TypeInfo Visit(WhileStmt stmt)
        {
            stmt.Condition.Solve(this);
            stmt.Body.Solve(this);
            return default;
        }
        public TypeInfo Visit(ForStmt stmt)
        {
            NewScope();
            stmt.Init.Solve(this);
            stmt.Condition.Solve(this);
            stmt.Update.Solve(this);
            stmt.Body.Solve(this);
            EndScope();
            return default;
        }
        public TypeInfo Visit(ReturnStmt stmt)
        {
            //自身のスコープの返り値と一致するか確認
            var type = stmt.ReturnExpr.Solve(this);
            if(_currentScopeReturnType != type.fullQualifiedName)
            {
                throw new SemanticsAnalyzeException(ErrorCode.InvalidTypeOfReturn,stmt.Position , stmt);
            }
            return default;
        }
        public TypeInfo Visit(BreakStmt stmt)
        {
            return default;
        }
        public TypeInfo Visit(ContinueStmt stmt)
        {
            return default;
        }
        public TypeInfo Visit(VarDeclStmt stmt)
        {
            // 型推論が必要な場合は、型推論を行う
            if (stmt.VarDecl.Type is null)
            {
                var type = stmt.VarDecl.InitValue.Solve(this);
                stmt.VarDecl.Type = type.fullQualifiedName;
            }
            // 型解決
            stmt.VarDecl.Solve(this);
            
            // ローカル変数を定義
            DefineLocalVariable(stmt.VarDecl, stmt.Position);
            return default;
        }
        public TypeInfo Visit(ExprStmt stmt)
        {
            stmt.Expr.Solve(this);
            return default;
        }
        

        //----------------
        // Expr
        //----------------
        public TypeInfo Visit(ConditionExpr expr)
        {
            var type = expr.Expr.Solve(this);
            PredefinedTypeName qualifiedTypeName = type.fullQualifiedName as PredefinedTypeName;
            if (qualifiedTypeName is null || !qualifiedTypeName.IsBool)
            {
                throw new SemanticsAnalyzeException(ErrorCode.InvalidTypeInCondition,expr.Position);
            }
            return type;
        }
        public TypeInfo Visit(AssignExpr expr)
        {
            var varType = expr.Tag.Solve(this);
            var valueType = expr.Value.Solve(this);
            
            // NG: immutableな変数への代入
            if (varType.isImmutable)
            {
                throw new SemanticsAnalyzeException(ErrorCode.CannotAssignToImmutableVariable, expr.Position, expr.Tag.Name);
            }
            
            // OK: 同じ型への代入
            if (varType.fullQualifiedName == valueType.fullQualifiedName)
            {
                expr.TypeName = varType.fullQualifiedName;
                return varType;
            }
            
            // OK: decimal <- int
            if (varType.fullQualifiedName == PrimitiveType.Float && valueType.fullQualifiedName == PrimitiveType.Int)
            {
                // decimal型への変換ノードを挿入
                expr.Value = new ConvertExpr(varType.fullQualifiedName, expr.Value, expr.Value.Position);
                expr.TypeName = varType.fullQualifiedName;
                return varType;
            }
            
            var t1 = _root.FindClass(varType.fullQualifiedName);
            if(t1 == null) throw new SemanticsAnalyzeException(ErrorCode.CannotConvertType,expr.Position, expr);
            
            // OK: 参照型へのnull代入
            if (valueType.fullQualifiedName == PrimitiveType.Null)
            {
                expr.TypeName = varType.fullQualifiedName;
                return varType;
            }
            
            // OK: class <- class.super
            if (IsSuperClassOf(valueType.fullQualifiedName, t1.Name))
            {
                expr.Value = new ConvertExpr(varType.fullQualifiedName, expr.Value, expr.Value.Position);
                expr.TypeName = varType.fullQualifiedName;
                return varType;
            }
            
            throw new SemanticsAnalyzeException(ErrorCode.CannotConvertType,expr.Position, expr);
        }
        public TypeInfo Visit(BinaryExpr expr)
        {
            TypeInfo resultType;
            var op = expr.Op;
            var type1 = expr.Left.Solve(this);
            var type2 = expr.Right.Solve(this);

            // 異なる型同士の暗黙的変換処理
            {
                // int -> decimalへの暗黙的型変換
                // (decimal)x <op> y
                if (type1.fullQualifiedName == PrimitiveType.Int && type2.fullQualifiedName == PrimitiveType.Float)
                {
                    expr.Left = new ConvertExpr(type2.fullQualifiedName, expr.Left, expr.Left.Position);
                    type1 = type2;
                }
                
                // x <op> (decimal)y
                else if (type1.fullQualifiedName == PrimitiveType.Float && type2.fullQualifiedName == PrimitiveType.Int)
                {
                    expr.Right = new ConvertExpr(type1.fullQualifiedName, expr.Right, expr.Right.Position);
                    type2 = type1;
                }
                
                // object <op> null の処理
                else if (type1.fullQualifiedName == PrimitiveType.Null && !type2.isPrimitive)
                {
                    type1 = type2;
                }
                else if (!type1.isPrimitive && type2.fullQualifiedName == PrimitiveType.Null)
                {
                    type2 = type1;
                }
            }

            // この時点で型が異なる場合は文法エラー
            if (type1 != type2)
            {
                throw new SemanticsAnalyzeException(ErrorCode.CannotConvertType,expr.Position, type1.fullQualifiedName, type2.fullQualifiedName);
            }
            
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
                    CheckCalcOperator(type1.fullQualifiedName, type2.fullQualifiedName, op, expr.Position);
                    resultType = type1;
                    break;
                
                case KesOperator.Eq:
                case KesOperator.NotEq:
                    CheckEqOperator(type1.fullQualifiedName, type2.fullQualifiedName, op, expr.Position);
                    resultType = new TypeInfo(PrimitiveType.Bool, true, true);
                    break;
                
                case KesOperator.Lt:
                case KesOperator.Gt:
                case KesOperator.Le:
                case KesOperator.Ge:
                    CheckCompareOperator(type1.fullQualifiedName, type2.fullQualifiedName, op, expr.Position);
                    resultType = new TypeInfo(PrimitiveType.Bool, true, true);
                    break;
                
                default:
                    throw new ArgumentException(op.ToString());
            }
            
            expr.TypeName = resultType.fullQualifiedName;
            return resultType;
        }
        public TypeInfo Visit(LogicExpr expr)
        {
            var type1 = expr.Left.Solve(this);
            var type2 = expr.Right.Solve(this);
            TypeInfo resultType;
            
            if (type1 != type2)
            {
                throw new SemanticsAnalyzeException(ErrorCode.CannotConvertType,expr.Position, type1.fullQualifiedName, type2.fullQualifiedName);
            }

            // 型チェック
            switch (expr.Op)
            {
                case KesOperator.LogAnd:
                case KesOperator.LogOr:
                    CheckLogicalOperator(type1.fullQualifiedName, type2.fullQualifiedName, expr.Op, expr.Position);
                    resultType = new TypeInfo(PrimitiveType.Bool, true, true);
                    break;
                default:
                    throw new ArgumentException(expr.Op.ToString());
            }

            expr.TypeName = resultType.fullQualifiedName;
            return resultType;
        }
        public TypeInfo Visit(UnaryExpr expr)
        {
            var op = expr.Op;
            var pos = expr.Position;
            var type = expr.Expr.Solve(this);

            switch (expr.Op)
            {
                case KesOperator.Minus: CheckMinusOperator(type.fullQualifiedName, op, pos); break;
                case KesOperator.Not: CheckNotOperator(type.fullQualifiedName, op, pos); break;
                case KesOperator.PreIncr: CheckIncrementOperator(type.fullQualifiedName, op, pos); break;
                case KesOperator.PreDecr: CheckIncrementOperator(type.fullQualifiedName, op, pos); break;
                default: throw new ArgumentException(op.ToString());
            }

            expr.TypeName = type.fullQualifiedName;
            return type;
        }
        public TypeInfo Visit(PrimaryExpr expr)
        {
            var op = expr.Op;
            var pos = expr.Position;
            var type = expr.Expr.Solve(this);
            
            switch (op)
            {
                case KesOperator.PostIncr:
                case KesOperator.PostDecr:
                    CheckIncrementOperator(type.fullQualifiedName, op, pos);
                    break;
                default: throw new ArgumentException(op.ToString());
            }
            
            expr.TypeName = type.fullQualifiedName;
            return type;
        }
        public TypeInfo Visit(IntegerLiteral integerLiteral)
        {
            throw new NotImplementedException();
        }
        public TypeInfo Visit(FloatLiteral floatLiteral)
        {
            throw new NotImplementedException();
        }
        public TypeInfo Visit(BooleanLiteral booleanLiteral)
        {
            throw new NotImplementedException();
        }
        public TypeInfo Visit(StringLiteral stringLiteral)
        {
            throw new NotImplementedException();
        }
        public TypeInfo Visit(NullLiteral nullLiteral)
        {
            throw new NotImplementedException();
        }
        public TypeInfo Visit(PredefinedTypeName predefinedTypeName)
        {
            throw new NotImplementedException();
        }
        
        // 変数の型解決
        public TypeInfo Visit(IdentifierName identifierName)
        {
            var name = identifierName.Name;
            
            // ローカル変数 -> クラス -> グローバル変数の順に探す
            var variable = _currentScope.FindVariable(name)
                                    ?? _currentAstClass?.FindField(name) 
                                    ?? _root.FindVariable(name);
            if (variable == null)
            {
                throw new SemanticsAnalyzeException(ErrorCode.UsedUndefinedSymbol, identifierName.Position, name);
            }

            identifierName.TypeName = variable.Type;
            identifierName.Decl = variable;

            if (identifierName.Next == null)
            {
                return new TypeInfo(variable.Type, variable.IsImmutable, PrimitiveType.IsPrimitiveType(variable.Type));
            }
            // チェーンしている場合
            return identifierName.Next.Solve(this);
        }
        public TypeInfo Visit(QualifiedName qualifiedName)
        {
            throw new NotImplementedException();
        }

        public TypeInfo Visit(FunctionCallExpr functionCallExpr)
        {
            // クラスメソッドテーブルから関数を探す
            var name = functionCallExpr.Name;
            var f = _currentAstClass?.FindMethod(name) ?? _root.FindFunction(name);
            
            if(f is null) throw new SemanticsAnalyzeException(ErrorCode.FunctionNotFound, functionCallExpr.Position, name);

            functionCallExpr.TypeName = f.ReturnType;
            
            foreach(var arg in functionCallExpr.Args)
            {
                arg.Solve(this);
            }
            
            if (functionCallExpr.Next == null)
            {
                return new TypeInfo(f.ReturnType, true, PrimitiveType.IsPrimitiveType(f.ReturnType));
            }
            // チェーンしている場合
            return functionCallExpr.Next.Solve(this);
        }
        
        public TypeInfo Visit(MemberAccessExpr memberAccessExpr)
        {
            var type = _root.FindClass(memberAccessExpr.Preview.TypeName);
            var field = type.FindField(memberAccessExpr.Name);
            if (field == null)
            {
                throw new SemanticsAnalyzeException(ErrorCode.FieldNotFound, memberAccessExpr.Position, type.Name, memberAccessExpr.Name);
            }
            memberAccessExpr.TypeName = type.Name;
            memberAccessExpr.Decl = field;
            
            if (memberAccessExpr.Next == null)
            {
                return new TypeInfo(field.Type, field.IsImmutable, PrimitiveType.IsPrimitiveType(field.Type));
            }
            return memberAccessExpr.Next.Solve(this);
        }
        
        public TypeInfo Visit(MethodCall methodCall)
        {
            var type = _root.FindClass(methodCall.Preview.TypeName);
            var method = type.FindMethod(methodCall.Name);
            if (method == null)
            {
                throw new SemanticsAnalyzeException(ErrorCode.MethodNotFound, methodCall.Position, type.Name, methodCall.Name);
            }
            methodCall.TypeName = method.ReturnType;

            foreach (var arg in method.Args)
            {
                arg.Solve(this);
            }
            
            if (methodCall.Next == null)
            {
                return new TypeInfo(method.ReturnType, true, PrimitiveType.IsPrimitiveType(method.ReturnType));
            }
            return methodCall.Next.Solve(this);
        }
        public TypeInfo Visit(ObjectCreationExpr objectCreationExpr)
        {
            CheckTypeExist(objectCreationExpr.TypeName, objectCreationExpr.Position);
            if (PrimitiveType.IsPrimitiveType(objectCreationExpr.TypeName))
            {
                throw new SemanticsAnalyzeException(ErrorCode.CannotCreateObjectAsPrimitiveType, objectCreationExpr.Position, objectCreationExpr.TypeName);
            }
            return new TypeInfo(objectCreationExpr.TypeName, false, false);
        }

        void CheckTypeExist(Expr type, CodePosition pos)
        {
            if(type is PredefinedTypeName) return;

            if (type is QualifiedName q)
            {
                throw new NotImplementedException();
            }
            
            if(type is IdentifierName i)
            {
                var t = _root.FindClass(i.Name);
                if (t is null)
                {
                    throw new SemanticsAnalyzeException(ErrorCode.TypeNotFound, pos, i.Name);
                }
            }
        }

        // targetがsrcのスーパークラスかどうかを判定
        bool IsSuperClassOf(string target, string src)
        {
            var srcClass = _root.FindClass(src);
            if (srcClass == null) return false;
            if (srcClass.SuperClass == target) return true;
            return IsSuperClassOf(target, srcClass.SuperClass);
        }
        
        void NewScope()
        {
            var scope = new Scope(_currentScope);
            _currentScope?.Children?.Add(scope);
            _currentScope = scope;
        }

        void EndScope()
        {
            _currentScope = _currentScope.Parent;
        }

        void DefineLocalVariable(AstVarDecl decl, CodePosition pos)
        {
            CheckTypeExist(decl.Type, pos);//有効な型かをチェック
            
            //二重定義チェック
            if (_currentScope.FindVariable(decl.Name) != null)
            {
                throw new SemanticsAnalyzeException(ErrorCode.VariableAlreadyDefined, pos, decl.Name);
            }
            
            _currentScope.DefineVariable(decl);
        }
        
        // +-*/%&|の演算子型チェック
        void CheckCalcOperator(string typename1, string typename2, KesOperator op, CodePosition pos)
        {
            if ((typename1 == PrimitiveType.Int || typename1 == PrimitiveType.Float)
                && (typename2 == PrimitiveType.Int || typename2 == PrimitiveType.Float))
            {
                return;
            }
            throw new SemanticsAnalyzeException(ErrorCode.InvalidTypeOfOperator, pos, op, typename1);
        }
        
        // ==, !=の演算子型チェック
        void CheckEqOperator(string typename1, string typename2, KesOperator op, CodePosition pos)
        {
            if (typename1 == typename2)
            {
                return;
            }
            throw new SemanticsAnalyzeException(ErrorCode.InvalidTypeOfOperator, pos, op, typename1);
        }
        
        // <, >, <=, >=の演算子型チェック
        void CheckCompareOperator(string typename1, string typename2, KesOperator op, CodePosition pos)
        {
            if ((typename1 == PrimitiveType.Int || typename1 == PrimitiveType.Float)
                && (typename2 == PrimitiveType.Int || typename2 == PrimitiveType.Float))
            {
                return;
            }
            throw new SemanticsAnalyzeException(ErrorCode.InvalidTypeOfOperator, pos, op, typename1);
        }
        
        // &&, ||の演算子型チェック
        void CheckLogicalOperator(string typename1, string typename2, KesOperator op, CodePosition pos)
        {
            if (typename1 == PrimitiveType.Bool && typename2 == PrimitiveType.Bool)
            {
                return;
            }
            throw new SemanticsAnalyzeException(ErrorCode.InvalidTypeOfOperator, pos, op, typename1);
        }
        
        // ++ --の演算子型チェック
        void CheckIncrementOperator(string typename, KesOperator op, CodePosition pos)
        {
            if (typename == PrimitiveType.Int)
            {
                return;
            }
            throw new SemanticsAnalyzeException(ErrorCode.InvalidTypeOfOperator, pos, op, typename);
        }
        
        // -の演算子型チェック
        void CheckMinusOperator(string typename, KesOperator op, CodePosition pos)
        {
            if (typename == PrimitiveType.Int || typename == PrimitiveType.Float)
            {
                return;
            }
            throw new SemanticsAnalyzeException(ErrorCode.InvalidTypeOfOperator, pos, op, typename);
        }
        
        // !の演算子チェック
        void CheckNotOperator(string typename, KesOperator op, CodePosition pos)
        {
            if (typename == PrimitiveType.Bool)
            {
                return;
            }
            throw new SemanticsAnalyzeException(ErrorCode.InvalidTypeOfOperator, pos, op, typename);
        }
        
        
    }
}
