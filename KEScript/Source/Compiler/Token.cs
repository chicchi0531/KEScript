namespace KESCompiler.Compiler
{
    public enum ETokenType
    {
        // 演算子
        Plus, // +
        Minus, // -
        Asterisk, // *
        Slash, // /
        Percent, // %
        Assign, // =
        Equal, // ==
        Not, // !
        NotEqual, // !=
        Less, // <
        LessEqual, // <=
        Greater, // >
        GreaterEqual, // >=
        And, // &&
        Or, // ||
        LogicalAnd, // &
        LogicalOr, // |
        PlusPlus, // ++
        MinusMinus, // --

        // リテラル
        Identifier, // 識別子
        StringLiteral, // 文字列
        IntegerLiteral, // 整数
        FloatLiteral, // 少数

        // グループ
        LeftParen, // (
        RightParen, // )
        LeftBrace, // {
        RightBrace, // }
        LeftBracket, // [
        RightBracket, // ]

        // その他
        Comma, // ,
        Dot, // .
        Semicolon, // ;
        Colon, // :
        Arrow, // ->

        // キーワード
        Fun, // 関数
        Var, // 変数
        Let, // 定数
        Class, //クラス
        If, // if
        Else, // else
        For, // for
        While, // while
        Return, // return
        Break, // break
        Continue, // continue
        True, // true
        False, // false
        Null, // null
        This, // this
        Switch, //switch
        Import, // import

        Int, // int
        String, // string
        Float, // decimal
        Bool, // bool

        Eof, // End Of File
        
        Public, // public
        Private, // private
        Protected, // protected
        Static, // static
        New, // new
    }

    public struct CodePosition
    {
        public string filename;
        public int line;
        public int count;

        public override string ToString()
        {
            return $"({line}:{count})";
        }

        public static CodePosition Unknown => new CodePosition()
        {
            filename = "", line = 0, count = 0
        };
    }

    public class Token
    {
        public Token(ETokenType type, object lexeme, string source, string filename, int line = 0,
            int count = 0)
        {
            Lexeme = lexeme;
            Type = type;
            Source = source;
            Position = new CodePosition
            {
                filename = filename,
                line = line,
                count = count
            };
        }
        public string Source { get; }
        public object Lexeme { get; }
        public ETokenType Type { get; }
        public CodePosition Position { get; }

        public override string ToString()
        {
            return $"[Token] Type:{Type}, Lexeme:\"{Lexeme}\", Source:\"{Source}\", Position:{Position}";
        }
    }
}
