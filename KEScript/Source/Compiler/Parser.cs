using KESCompiler.Compiler.Ast;

namespace KESCompiler.Compiler
{
    public class Parser
    {
        readonly ILogger _logger;
        readonly Token[] _tokens;
        int _localVariableIndex = 0;
        int _currentPos = 0;

        public Parser(ILogger logger, Token[] tokens)
        {
            _logger = logger;
            _tokens = tokens;
        }

        public ProgramNode Parse()
        {
            return ParseProgram();
        }

        //====================
        // トップレベル構文の構文解析
        //====================
        ProgramNode ParseProgram()
        {
            var classes = new List<ClassDecl>();
            var functions = new List<FunctionDecl>();
            var variables = new List<VarDecl>();
            
            while (!ReadAllProgram())
            {
                // アクセス修飾子の解析
                // ("public" | "private" | "protected")?
                EAccessModifier accessModifier = EAccessModifier.Private;
                if (TryMatch(ETokenType.Public))
                    accessModifier = EAccessModifier.Public;
                else if (TryMatch(ETokenType.Protected))
                    accessModifier = EAccessModifier.Protected;
                else if (TryMatch(ETokenType.Private)) accessModifier = EAccessModifier.Private;
                
                // トップレベル構文の解析
                try
                {
                    if (TryMatch(ETokenType.Class))
                        classes.Add(ParseClassDecl(accessModifier));
                    else if (TryMatch(ETokenType.Fun))
                    {
                        functions.Add(ParseFunctionDecl(accessModifier));
                    }
                    else if (TryMatch(ETokenType.Var))
                    {
                        variables.Add(ParseVarDecl(false, variables.Count, EVarScope.ClassField, accessModifier));
                        Consume(ETokenType.Semicolon, ErrorCode.SemicolonExpected);
                    }
                    else if (TryMatch(ETokenType.Let))
                    {
                        variables.Add(ParseVarDecl(true, variables.Count, EVarScope.ClassField, accessModifier));
                        Consume(ETokenType.Semicolon, ErrorCode.SemicolonExpected);
                    }
                    else
                    {
                        throw new CompileErrorException(ErrorCode.TopLevelStatementNotAllowed);
                    }
                }
                catch (CompileErrorException e)
                {
                    ParseError(e);
                    TrySynchronous();
                }

                if (Peek().Type == ETokenType.Eof) Advance();
            }
            
            //型辞書の作成
            foreach (var c in classes)
            {
                
            }
            
            return new ProgramNode(new CodePosition(),
                classes.ToArray(),
                functions.ToArray(),
                variables.ToArray()
                );
        }

        // クラス定義の構文解析
        ClassDecl ParseClassDecl(EAccessModifier accessModifier)
        {
            // classキーワードは消費済みなので、それ以後のトークンを解析する
            // "class" <identifier>
            var className = Consume(ETokenType.Identifier, ErrorCode.IdentifierExpected)?.Lexeme.ToString() ?? "";
            
            // 継承リストの解析
            // (':' <identifier> (',' <identifier>)* )?
            Expr? superClass = null;
            if (TryMatch(ETokenType.Colon))
            {
                superClass = ParseTypeName();
            }

            // <class_block>
            var classBody = ParseClassBlock();

            return new ClassDecl(
                className,
                accessModifier,
                superClass,
                Previous().Position,
                 classBody.functions, classBody.variables
                );
        }

        // クラスブロックの構文解析
        (FunctionDecl[] functions, VarDecl[] variables) ParseClassBlock()
        {
            Consume(ETokenType.LeftBrace, ErrorCode.LeftBraceExpected);

            List<FunctionDecl> functions = new();
            List<VarDecl> variables = new();
            while (!TryMatch(ETokenType.RightBrace))
            {
                if (IsEndOfFile())
                {
                    throw new CompileErrorException(ErrorCode.RightBraceExpected);
                }
                try
                {
                    ParseClassMemberDecl(functions, variables);
                }
                catch (CompileErrorException e)
                {
                    if (!TrySynchronous()) throw;
                }
            }
            return (functions.ToArray(), variables.ToArray());
        }

        // クラスメンバーひとつぶんの構文解析
        void ParseClassMemberDecl(List<FunctionDecl> functions, List<VarDecl> variables)
        {
            // アクセス修飾子
            // ("public" | "private" | "protected")?
            EAccessModifier accessModifier = EAccessModifier.Private;
            if (TryMatch(ETokenType.Public))
            {
                accessModifier = EAccessModifier.Public;
            }
            else if (TryMatch(ETokenType.Protected))
            {
                accessModifier = EAccessModifier.Protected;
            }
            else if (TryMatch(ETokenType.Private))
            {
                accessModifier = EAccessModifier.Private;
            }

            // クラスメンバーの解析
            // (<variable_decl> | <function_decl>);
            // <variable_decl> ::= 
            //      "var" <identifier> ':' <type> ';' ||
            //      "let" <identifier> ':' <type> ';' ;
            if (TryMatch(ETokenType.Var))
            {
                variables.Add(ParseVarDecl(false, variables.Count, EVarScope.ClassField, accessModifier));
                Consume(ETokenType.Semicolon, ErrorCode.SemicolonExpected);
            }
            else if (TryMatch(ETokenType.Let))
            {
                variables.Add(ParseVarDecl(true, variables.Count, EVarScope.ClassField, accessModifier));
                Consume(ETokenType.Semicolon, ErrorCode.SemicolonExpected);
            }
            else if (TryMatch(ETokenType.Fun))
            {
                functions.Add(ParseFunctionDecl(accessModifier));
            }
            else
            {
                throw new CompileErrorException(ErrorCode.InvalidDeclaration);
            }
        }

        // 関数定義の構文解析
        FunctionDecl ParseFunctionDecl(EAccessModifier accessModifier)
        {
            var pos = Previous().Position;

            // funcキーワードは消費済みなので、それ以後のトークンを解析する
            // "func" <identifier>
            var funcName = Consume(ETokenType.Identifier, ErrorCode.IdentifierExpected)?.Lexeme.ToString();

            // 引数リストの解析
            // '(' <parameter> (',' <parameter>)* ')'
            // <parameter> ::= <identifier> : <type>
            Consume(ETokenType.LeftParen, ErrorCode.LeftParenthesisExpected);
            var argList = new List<VarDecl>();
            int argIndex = 1;//0がthisなので、1から始める
            while (Peek().Type == ETokenType.Identifier)
            {
                argList.Add(ParseVarDecl(false, argIndex++, EVarScope.FunctionArg));
                if (!TryMatch(ETokenType.Comma)) break;
            }
            Consume(ETokenType.RightParen, ErrorCode.RightParenthesisExpected);

            // 戻り値の解析
            // "->" <type>
            Expr t = null;
            if (TryMatch(ETokenType.Arrow))
            {
                t = ParseTypeName();
            }
            
            // 関数の中身の解析
            // <block>
            _localVariableIndex = 0;
            var block = ParseBlock();

            return new FunctionDecl(accessModifier, t,funcName, argList.ToArray(), block, pos);
        }

        // 変数定義の構文解析
        VarDecl ParseVarDecl(bool isImmutable, int address, EVarScope scope, EAccessModifier accessModifier = EAccessModifier.Private)
        {
            // varキーワードは消費済みなので、それ以後のトークンを解析する
            // ("var" | "let") <identifier> ':' <type> ';'
            var name = Consume(ETokenType.Identifier, ErrorCode.IdentifierExpected)?.Lexeme.ToString();
            Consume(ETokenType.Colon, ErrorCode.CoronExpected);
            var t = ParseTypeName();

            return new VarDecl(accessModifier, 
                t,
                name,
                null,
                isImmutable,
                address,
                scope,
                Previous().Position);
        }
        
        Expr ParseTypeName()
        {
            // primitive型
            if(Peek().Type is
                ETokenType.Bool 
                or ETokenType.Float 
                or ETokenType.Int 
                or ETokenType.String)
            {
                Advance();
                return new PredefinedTypeName(Peek(), Peek().Position);
            }
            
            // class型
            CodePosition pos = Peek().Position;
            var typename = Consume(ETokenType.Identifier, ErrorCode.TypeExpected)?.Lexeme.ToString();
            Expr result = new IdentifierName(typename, Peek().Position);
            while (TryMatch(ETokenType.Dot))
            {
                typename = Consume(ETokenType.Identifier, ErrorCode.TypeExpected)?.Lexeme.ToString();
                result = new ModuleName(result, new IdentifierName(typename, Peek().Position), pos);
            }
            return result;
        }

        //====================
        // 文の構文解析
        //====================
        BlockStmt ParseBlock()
        {
            Consume(ETokenType.LeftBrace, ErrorCode.LeftBraceExpected);
            var stmts = new List<Stmt>();
            while (!IsEndOfFile())
            {
                try
                {
                    if (TryMatch(ETokenType.RightBrace)) return new BlockStmt(stmts.ToArray(), Peek().Position);
                    stmts.Add(ParseStmt());
                }
                catch (CompileErrorException e)
                {
                    ParseError(e);
                    TrySynchronous();
                }
            }
            throw new CompileErrorException(ErrorCode.RightBraceExpected);
        }

        Stmt ParseStmt()
        {
            Stmt result;
            switch (Peek().Type)
            {
                case ETokenType.Var or ETokenType.Let:
                    result = ParseVarDeclStmt();
                    break;
                case ETokenType.While: result = ParseWhileStmt();
                    break;
                case ETokenType.For: result = ParseForStmt();
                    break;
                case ETokenType.If: result = ParseIfStmt();
                    break;
                case ETokenType.Return: result = ParseReturnStmt();
                    break;
                case ETokenType.Break: result = ParseBreakStmt();
                    break;
                case ETokenType.Continue: result = ParseContinueStmt();
                    break;
                default: result = ParseExprStmt();
                    break;
            }
            return result;
        }

        VarDeclStmt ParseVarDeclStmt()
        {
            var pos = Peek().Position;
            
            // var,letキーワード
            bool isConstance = false;
            if (TryMatch(ETokenType.Var)) isConstance = false;
            else if (TryMatch(ETokenType.Let)) isConstance = true;
            else
            {
                throw new Exception("Unknown error occured at ParseVarDeclStmt.");
            }
            
            // 変数名
            var name = Consume(ETokenType.Identifier, ErrorCode.IdentifierExpected)?.Lexeme.ToString();

            // 型名
            Expr type = null;
            if (TryMatch(ETokenType.Colon))
            {
                type = ParseTypeName();
            }
            
            // 初期値
            Expr initValue = null;
            if (TryMatch(ETokenType.Assign))
            {
                initValue = ParseExpr();
                if (initValue is null)
                {
                    throw new CompileErrorException(ErrorCode.ExpressionExpected);
                }
            }
            
            if(type is null && initValue is null)
            {
                throw new CompileErrorException(ErrorCode.ImplicitlyTypeMustBeInitialized);
            }
            Consume(ETokenType.Semicolon, ErrorCode.RightBraceExpected);

            var varDecl = new VarDecl(
                EAccessModifier.Private,
                type,
                name,
                initValue,
                isConstance,
                _localVariableIndex++,
                EVarScope.Local,
                pos);
            return new VarDeclStmt(varDecl, pos);
        }

        Stmt ParseWhileStmt()
        {
            var pos = Peek().Position;
            Consume(ETokenType.While, ErrorCode.WhileKeywordExpected);
            Consume(ETokenType.LeftParen, ErrorCode.LeftParenthesisExpected);
            var condition = ParseConditionExpr();
            Consume(ETokenType.RightParen, ErrorCode.RightParenthesisExpected);
            var body = ParseBlock();

            return new WhileStmt(condition, body, pos);
        }
        
        Stmt ParseForStmt()
        {
            var pos = Peek().Position;
            Consume(ETokenType.For, ErrorCode.ForKeywordExpected);
            Consume(ETokenType.LeftParen, ErrorCode.LeftParenthesisExpected);
            var initCondition = ParseForInitializer();
            var condition = ParseConditionExpr();
            Consume(ETokenType.Semicolon, ErrorCode.SemicolonExpected);
            var updateCondition = ParseExpr();
            Consume(ETokenType.RightParen, ErrorCode.RightParenthesisExpected);
            var body = ParseBlock();
            
            return new ForStmt(initCondition, condition, updateCondition, body, pos);
        }

        Stmt ParseForInitializer()
        {
            Stmt result;
            switch (Peek().Type)
            {
                case ETokenType.Var or ETokenType.Let:
                    result = ParseVarDeclStmt();
                    break;
                case ETokenType.While: 
                case ETokenType.For: 
                case ETokenType.If: 
                case ETokenType.Return:
                case ETokenType.Break:
                case ETokenType.Continue:
                    throw new CompileErrorException(ErrorCode.ForInitializerExpectVarDeclOrExpr);;
                default: result = ParseExprStmt();
                    break;
            }
            return result;
        }
        
        Stmt ParseIfStmt()
        {
            var pos = Peek().Position;
            Consume(ETokenType.If, ErrorCode.IfKeywordExpected);
            Consume(ETokenType.LeftParen, ErrorCode.LeftParenthesisExpected);
            var condition = ParseConditionExpr();
            Consume(ETokenType.RightParen, ErrorCode.RightParenthesisExpected);
            var thenBody = ParseBlock();

            Stmt elseBody = null;
            if (TryMatch(ETokenType.Else))
            {
                if (Peek().Type == ETokenType.If)
                {
                    // else ifの場合
                    elseBody = ParseIfStmt();
                }
                else
                {
                    elseBody = ParseBlock();
                }
            }
            
            return new IfStmt(condition, thenBody, elseBody, pos);
        }
        
        Stmt ParseReturnStmt()
        {
            var pos = Peek().Position;
            Consume(ETokenType.Return, ErrorCode.ReturnKeywordExpected);
            var returnExpr = ParseExpr();

            Consume(ETokenType.Semicolon, ErrorCode.SemicolonExpected);
            
            return new ReturnStmt(returnExpr, pos);
        }
        
        Stmt ParseBreakStmt()
        {
            var pos = Peek().Position;
            Consume(ETokenType.Break, ErrorCode.BreakKeywordExpected);
            Consume(ETokenType.Semicolon, ErrorCode.SemicolonExpected);
            return new BreakStmt(pos);
        }
        
        Stmt ParseContinueStmt()
        {
            var pos = Peek().Position;
            Consume(ETokenType.Continue, ErrorCode.ContinueKeywordExpected);
            Consume(ETokenType.Semicolon, ErrorCode.SemicolonExpected);
            return new ContinueStmt(pos);
        }

        // <expr_stmt> ::= <expr> ";";
        ExprStmt ParseExprStmt()
        {
            var expr = ParseExpr();
            Consume(ETokenType.Semicolon, ErrorCode.SemicolonExpected);
            return new ExprStmt(expr, expr.Position);
        }

        //====================
        // 式の構文解析
        //====================

        // <expr> ::= <logic_or>;
        Expr ParseExpr()
        {
            return ParseAssign();
        }

        ConditionExpr ParseConditionExpr()
        {
            var pos = Peek().Position;
            return new ConditionExpr(ParseLogicOr(), pos);
        }

        // <assign> ::= <logic_or> ( "=" <logic_or> )*;
        Expr ParseAssign()
        {
            var left = ParseLogicOr();

            if (TryMatch(ETokenType.Assign))
            {
                var op = Previous();
                var right = ParseAssign(); //右結合にするため、再帰的にAssignを呼び出す
                if (right == null)
                {
                    throw new CompileErrorException(ErrorCode.InvalidExprTerm);
                }
                
                //leftが左辺値であるかどうかチェックする
                //終端ノードが、変数、配列要素、メンバーアクセスのいずれかであれば、左辺値である
                var isLeftValue = left is MemberAccessExpr or IdentifierName;
                if (!isLeftValue)
                {
                    throw new CompileErrorException(ErrorCode.LeftOfAssignMustBeLeftValue);
                }
                
                left = new AssignExpr(left, right, left.Position);
            }

            return left;
        }

        // <logic_or> ::= <logic_and> ( "&&" <logic_and>)*;
        Expr ParseLogicOr()
        {
            var left = ParseLogicAnd();
            if (left == null)
            {
                return null;
            }

            while (TryMatch(ETokenType.And))
            {
                var op = Previous();
                var right = ParseLogicAnd();
                if (right == null)
                {
                    throw new CompileErrorException(ErrorCode.InvalidExprTerm);
                }
                left = new LogicExpr(left, op, right, left.Position);
            }

            return left;
        }

        // <logic_and> ::= <equality> ( "||" <equality>)*;
        Expr ParseLogicAnd()
        {
            var left = ParseEquality();
            if (left == null)
            {
                return null;
            }

            while (TryMatch(ETokenType.Or))
            {
                var op = Previous();
                var right = ParseEquality();
                if (right == null)
                {
                    throw new CompileErrorException(ErrorCode.InvalidExprTerm);
                }
                left = new LogicExpr(left, op, right, left.Position);
            }

            return left;
        }

        // <equality> ::= <term> (("==" | "!=" | "<" | ">" | "<=" | ">=" ) <term>)*;
        Expr ParseEquality()
        {
            var left = ParseTerm();
            if (left == null) return null;

            while (TryMatch(ETokenType.Equal) || TryMatch(ETokenType.NotEqual)
                || TryMatch(ETokenType.Greater) || TryMatch(ETokenType.GreaterEqual)
                || TryMatch(ETokenType.Less) || TryMatch(ETokenType.LessEqual))
            {
                var op = Previous();
                var right = ParseTerm();
                if (right == null)
                {
                    throw new CompileErrorException(ErrorCode.InvalidExprTerm);
                }
                left = new BinaryExpr(left, op, right, left.Position);
            }
            return left;
        }

        // <term> ::= <factor> (('+' | '-') <factor>)*;
        Expr ParseTerm()
        {
            var left = ParseFactor();
            if (left == null) return null;

            while (TryMatch(ETokenType.Plus) || TryMatch(ETokenType.Minus))
            {
                var op = Previous();
                var right = ParseFactor();
                if (right == null)
                {
                    throw new CompileErrorException(ErrorCode.InvalidExprTerm);
                }
                left = new BinaryExpr(left, op, right, left.Position);
            }
            return left;
        }

        // <factor> ::= <unary> (('*' | '/') <unary>)*;
        Expr ParseFactor()
        {
            var left = ParseUnary();
            while (TryMatch(ETokenType.Asterisk) || TryMatch(ETokenType.Slash) || TryMatch(ETokenType.Percent))
            {
                var op = Previous();
                var right = ParseUnary();
                if (right == null)
                {
                    throw new CompileErrorException(ErrorCode.InvalidExprTerm);
                }
                left = new BinaryExpr(left, op, right, left.Position);
            }
            return left;
        }

        // <unary> ::= <primary> | ('!' | '-') <unary>;
        Expr ParseUnary()
        {
            if (TryMatch(ETokenType.Minus)
                || TryMatch(ETokenType.Not)
                || TryMatch(ETokenType.PlusPlus)
                || TryMatch(ETokenType.MinusMinus))
            {
                var op = Previous();
                var left = ParseUnary();
                if (left == null)
                {
                    throw new CompileErrorException(ErrorCode.InvalidExprTerm);
                }
                return new UnaryExpr(left, op, left.Position);
            }
            return ParsePrimary();
        }

        // <primary> ::= <number> | <string> | <bool> | <identifier> | <group>;
        Expr ParsePrimary()
        {
            // 定数リテラル
            Token t;
            switch (Peek().Type)
            {
                case ETokenType.IntegerLiteral: t = Advance();
                    return new IntegerLiteral((int)t.Lexeme, t.Position);
                case ETokenType.FloatLiteral: t = Advance();
                    return new FloatLiteral((float)t.Lexeme, t.Position);
                case ETokenType.StringLiteral: t = Advance();
                    return new StringLiteral((string)t.Lexeme, t.Position);
                case ETokenType.True: t = Advance();
                    return new BooleanLiteral(true, t.Position);
                case ETokenType.False: t = Advance();
                    return new BooleanLiteral(false, t.Position);
                case ETokenType.Null: t = Advance();
                    return new NullLiteral(t.Position);
            }

            // 変数・関数
            if (Peek().Type == ETokenType.Identifier)
            {
                var expr =  ParseMemberAccess(null);
                if (TryMatch(ETokenType.PlusPlus) || TryMatch(ETokenType.MinusMinus))
                {
                    var op = Previous();
                    return new PrimaryExpr(expr, op, expr.Position);
                }
                return expr;
            }
            
            // newオブジェクト
            if (Peek().Type == ETokenType.New)
            {
                return ParseNewObject();
            }

            // カッコ
            if (TryMatch(ETokenType.LeftParen))
            {
                var expr = ParseExpr();
                Consume(ETokenType.RightParen, ErrorCode.RightParenthesisExpected);
                return expr;
            }

            return null;
        }
        
        ObjectCreationExpr ParseNewObject()
        {
            var pos = Peek().Position;
            Consume(ETokenType.New, ErrorCode.NewKeywordExpected);
            
            var typename = ParseTypeName();

            //通常new
            if (TryMatch(ETokenType.LeftParen))
            {
                var args = ParseArgList();
                return new ObjectCreationExpr(typename, args, pos);
            }
            
            // 配列new
            if (TryMatch(ETokenType.LeftBracket))
            {
                // todo:配列の初期化ノードを作成する
                // var arg = ParseExpr();
                // return new ArrayCreationExpr(typename, arg, pos);
                throw new NotImplementedException();
            }
            
            throw new CompileErrorException(ErrorCode.LeftParenthesisExpected);
        }

        List<Expr> ParseArgList()
        {
            //引数がからの場合はからのリストを返す
            if (TryMatch(ETokenType.RightParen)) return new();
            
            var argList = new List<Expr>();
            while (true)
            {
                if (IsEndOfFile())
                {
                    throw new CompileErrorException(ErrorCode.RightParenthesisExpected);
                }

                var expr = ParseExpr();
                if (expr == null)
                {
                    throw new CompileErrorException(ErrorCode.ExpressionExpected);
                }
                argList.Add(expr);

                // 引数リストの終端かどうかをチェック。コンマが出るなら次の引数があるとみなし、続ける
                if (TryMatch(ETokenType.RightParen)) break;
                Consume(ETokenType.Comma, ErrorCode.CommaExpected);
            }
            return argList;
        }

        Expr ParseMemberAccess(Expr? preview)
        {
            var t = Consume(ETokenType.Identifier, ErrorCode.IdentifierExpected);
            var name = t.Lexeme.ToString();
            
            // メンバーアクセスの場合
            Expr? expr = null;
            if (preview is not null)
            {
                expr = new MemberAccessExpr(name, preview, t.Position);
            }

            //関数呼び出し
            if (TryMatch(ETokenType.LeftParen))
            {
                var args = ParseArgList();
                expr = new FunctionCallExpr(
                    expr ?? new IdentifierName(name, t.Position),
                    args,
                    t.Position
                    );
            }
            // 変数アクセス
            else
            {
                expr ??= new IdentifierName(name, t.Position);
            }

            // 次の要素に繋がっている場合
            if(TryMatch(ETokenType.Dot))
            {
                expr = ParseMemberAccess(expr);
            }
            
            return expr;
        }

        bool IsEndOfFile()
        {
            return Peek().Type == ETokenType.Eof;
        }

        bool ReadAllProgram()
        {
            return _tokens.Length == _currentPos;
        }

        Token Advance()
        {
            return _tokens[_currentPos++];
        }

        Token Previous()
        {
            return _tokens[_currentPos - 1];
        }

        Token Rewind()
        {
            return _tokens[_currentPos--];
        }

        Token Peek()
        {
            return _tokens[_currentPos];
        }

        Token PeekNext()
        {
            return _tokens[_currentPos + 1];
        }

        bool TryMatch(ETokenType t)
        {
            if (Peek().Type == t)
            {
                Advance();
                return true;
            }
            return false;
        }

        void ParseError(CompileErrorException e)
        {
            _logger.Error(Peek().Position, e.ErrorInfo, e.OptionData);
        }

        // 指定のトークンとマッチした場合は、そのトークンを。失敗した場合はエラーをスローする
        Token Consume(ETokenType type, KesErrorType errorType)
        {
            if (Peek().Type == type)
            {
                return Advance();
            }

            throw new CompileErrorException(errorType);
        }

        // エラーが発生したときに、エラー地点を超えて解析が続けられる地点まで飛ばす
        bool TrySynchronous()
        {
            while (!TryMatch(ETokenType.Semicolon))
            {
                if (IsEndOfFile()) return false;
                Advance();
            }
            return true;
        }
    }


}
