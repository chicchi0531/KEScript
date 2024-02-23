namespace KESCompiler.Compiler;

public class Lexer(ILogger logger)
{
    readonly Dictionary<string, ETokenType> _keywords = new()
    {
        { "func", ETokenType.Fun },
        { "var", ETokenType.Var },
        { "let", ETokenType.Let },
        { "class", ETokenType.Class },
        { "if", ETokenType.If },
        { "else", ETokenType.Else },
        { "for", ETokenType.For },
        { "while", ETokenType.While },
        { "return", ETokenType.Return },
        { "switch", ETokenType.Switch },
        { "break", ETokenType.Break },
        { "continue", ETokenType.Continue },
        { "this", ETokenType.This },
        { "int", ETokenType.Int },
        { "string", ETokenType.String },
        { "float", ETokenType.Float },
        { "bool", ETokenType.Bool },
        { "public", ETokenType.Public },
        { "private", ETokenType.Private },
        { "protected", ETokenType.Protected },
        { "static", ETokenType.Static },
        { "new", ETokenType.New },
        { "import", ETokenType.Import },
    };
    int _current;
    string _filename = "";
    int _line;
    string _source = "";
    int _start;
    int _startLine;
    List<Token> _tokens = [];

    int Count => _current - _startLine;

    public Token[] Lex(string input, string filename)
    {
        logger.Log($"字句解析を開始します。");
        _filename = filename;
        _source = input;
        _tokens = new List<Token>();

        _line = 1;
        _current = 0;

        while (!IsEnd())
        {
            try
            {
                _start = _current;
                ScanToken();
            }
            catch (CompileErrorException e)
            {
                var pos = new CodePosition()
                {
                    count = Count,
                    filename = _filename,
                    line = _line
                };
                var err = e.ErrorInfo;
                logger.Error(pos,err);

                TrySynchronous(); //復帰に挑戦
            }
            catch (Exception)
            {
                logger.Error(new CodePosition()
                {
                    count = Count,
                    filename = _filename,
                    line = _line
                }, ErrorCode.UnknownError);
                throw; //unexpected error
            }
        }
        AddToken(ETokenType.Eof);
        if (logger.ErrorCount > 0)
        {
            logger.Log("❌字句解析に失敗しました。エラー内容を確認してください。");
            return [];
        }

        logger.Log("✅字句解析が正常に終了しました。");
        return _tokens.ToArray();
    }

    void ScanToken()
    {
        // 不要な文字のスキップ
        while (SkipSpaceAndNewLine() || SkipComment()) {}
        if (IsEnd()) return;

        // 各トークンの読み出し
        _start = _current;
        var c = Advance();
        switch (c)
        {
            case '+':
                AddToken(Match('+') ? ETokenType.PlusPlus : ETokenType.Plus);
                break;
            case '-':
                AddToken(
                    Match('>') ? ETokenType.Arrow
                    : Match('-') ? ETokenType.MinusMinus
                    : ETokenType.Minus
                );
                break;
            case '*':
                AddToken(ETokenType.Asterisk);
                break;
            case '/':
                AddToken(ETokenType.Slash);
                break;
            case '%':
                AddToken(ETokenType.Percent);
                break;

            case '=':
                AddToken(Match('=') ? ETokenType.Equal : ETokenType.Assign);
                break;
            case '!':
                AddToken(Match('=') ? ETokenType.NotEqual : ETokenType.Not);
                break;
            case '<':
                AddToken(Match('=') ? ETokenType.LessEqual : ETokenType.Less);
                break;
            case '>':
                AddToken(Match('=') ? ETokenType.GreaterEqual : ETokenType.Greater);
                break;
            case '&':
                AddToken(Match('&') ? ETokenType.And : ETokenType.LogicalAnd);
                break;
            case '|':
                AddToken(Match('|') ? ETokenType.Or : ETokenType.LogicalOr);
                break;

            case '(':
                AddToken(ETokenType.LeftParen);
                break;
            case ')':
                AddToken(ETokenType.RightParen);
                break;
            case '{':
                AddToken(ETokenType.LeftBrace);
                break;
            case '}':
                AddToken(ETokenType.RightBrace);
                break;
            case '[':
                AddToken(ETokenType.LeftBracket);
                break;
            case ']':
                AddToken(ETokenType.RightBracket);
                break;

            case ',':
                AddToken(ETokenType.Comma);
                break;
            case '.':
                AddToken(ETokenType.Dot);
                break;
            case ';':
                AddToken(ETokenType.Semicolon);
                break;
            case ':':
                AddToken(ETokenType.Colon);
                break;

            case '"':
                var stringLiteral = ReadStringLiteral();
                AddToken(ETokenType.StringLiteral, stringLiteral);
                break;

            default:
                // number literal
                if (char.IsDigit(c))
                {
                    var num = ReadNumber();
                    switch (num)
                    {
                        case int:
                            AddToken(ETokenType.IntegerLiteral, num);
                            break;
                        case float:
                            AddToken(ETokenType.FloatLiteral, num);
                            break;
                        default: throw new CompileErrorException(ErrorCode.FailedToReadNumberLiteral, num);
                    }
                }

                // identifier or keyword
                else if (char.IsLetter(c))
                {
                    var identifier = ReadIdentifier();
                    if (_keywords.TryGetValue(identifier, out var keyword))
                    {
                        AddToken(keyword, identifier); //キーワードの場合
                    }
                    else if (identifier == "true")
                    {
                        AddToken(ETokenType.True, true);
                    }
                    else if (identifier == "false")
                    {
                        AddToken(ETokenType.False, false);
                    }
                    else if (identifier == "null")
                    {
                        AddToken(ETokenType.Null);
                    }
                    else
                    {
                        AddToken(ETokenType.Identifier, identifier); //識別子の場合
                    }
                }
                else
                {
                    //　不明なトークンエラー
                    throw new CompileErrorException(ErrorCode.AppearedInvalidToken);
                }
                break;
        }
    }

    void AddToken(ETokenType type, object lexeme = null)
    {
        _tokens.Add(new Token(type, lexeme, _source.Substring(_start, _current - _start), _filename, _line,
            Count));
    }

    // 数字リテラルを読み込み、読んだ文字列を返す
    object ReadNumber()
    {
        for (var c = Peek(); char.IsDigit(c); c = Peek())
        {
            Advance();
        }

        if (!Match('.'))
        {
            var numStr = _source.Substring(_start, _current - _start);
            return int.Parse(numStr);
        }
        Advance(); // 小数点'.'の読み飛ばし

        // 小数点以下の読み出し
        for (var c = Peek(); char.IsDigit(c); c = Peek())
        {
            Advance();
        }
        var floatStr = _source.Substring(_start, _current - _start);

        if (float.TryParse(floatStr, out var num)) return null;
        return num;
    }

    // 識別子を読み込み、読んだ文字列を返す
    string ReadIdentifier()
    {
        for (var c = Peek();
            char.IsLetterOrDigit(c) || c is '_';
            c = Peek())
        {
            Advance();
        }
        return _source.Substring(_start, _current - _start);
    }

    // ""で囲まれた文字列を読み込み、読んだ文字列から""を除いて返す
    string ReadStringLiteral()
    {
        bool StringLiteralErrorCheck()
        {
            if (IsEnd())
            {
                throw new CompileErrorException(ErrorCode.StringLiteralNotClosed);
            }

            if (Peek() == '\n')
            {
                throw new CompileErrorException(ErrorCode.LineBreakDetectedInTheStringLiteral);
            }

            return true;
        }

        var str = "";
        for (var c = Peek(); c != '"'; c = Peek())
        {
            // エスケープ文字の場合、次の文字は無条件で読み込む
            if (c == '\\')
            {
                // \の読み飛ばし
                Advance();
                if (!StringLiteralErrorCheck()) return "";

                str += '\\' + Peek();

                Advance(); // \の次の文字の読み飛ばし
                c = Peek();
            }

            if (!StringLiteralErrorCheck()) return "";
            str += c;
            Advance();
        }
        Advance(); // "の読み飛ばし

        return str;
    }

    void TrySynchronous()
    {
        while (Peek() != '\n' && !IsEnd())
        {
            Advance();
        }
    }

    // 現在の文字が終端に達しているかどうかを返す
    bool IsEnd()
    {
        if (_current >= _source.Length) return true;
        return false;
    }

    // 解析を一文字進め、進める前の文字を返す
    char Advance()
    {
        return _source[_current++];
    }

    // 現在の文字が引数の文字と一致したら解析をひとつ進める
    bool Match(char c)
    {
        if (IsEnd()) return false;
        if (Peek() != c) return false;

        Advance();
        return true;
    }

    // 空白文字と改行をスキップする
    bool SkipSpaceAndNewLine()
    {
        var hasSkipped = false;

        var c = Peek();
        while (c is '\n' or '\t' or '\r'
            || char.IsWhiteSpace(c))
        {
            if (c == '\n') NewLine();

            Advance();

            c = Peek();
            hasSkipped = true;
        }
        return hasSkipped;
    }

    // コメントをスキップする
    bool SkipComment()
    {
        var hasSkipped = false; //何かしらをスキップしたかどうか

        // 行コメントスキップ
        if (Peek() == '/' && PeekNext() == '/')
        {
            while (Peek() != '\n')
            {
                if (IsEnd()) return true;
                Advance();
            }
            hasSkipped = true;
        }

        // 複数行コメントスキップ
        if (Peek() == '/' && PeekNext() == '*')
        {
            Advance(); // /の読み飛ばし
            Advance(); // *の読み飛ばし
            while (Peek() != '*' && PeekNext() != '/')
            {
                if (IsEnd())
                {
                    throw new CompileErrorException(ErrorCode.CommentBlockNotClosed);
                }
                else if(Peek() == '\n') NewLine();
                Advance();
            }
            Advance(); // *の読み飛ばし
            Advance(); // /の読み飛ばし
            hasSkipped = true;
        }
        return hasSkipped;
    }

    // 現在の文字を返す
    char Peek()
    {
        if (IsEnd()) return '\0';
        return _source[_current];
    }

    // 次の文字を返す
    char PeekNext()
    {
        if (_current + 1 >= _source.Length) return '\0';
        return _source[_current + 1];
    }

    // 行カウントを進める
    void NewLine()
    {
        _line++;
        _startLine = _current;
    }
}