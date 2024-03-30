namespace KESCompiler.Compiler;

[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = true)]
public sealed class KesErrorAttribute : Attribute
{
    KesErrorType _errorInfo;
    public KesErrorAttribute(int id, string name, string message)
    {
        _errorInfo = new(id, name, message);
    }
}

public struct KesErrorType
{
    public KesErrorType(int id, string name, string message)
    {
        this.id = id;
        this.name = name;
        this.message = message;
    }
    public int id;
    public string name;
    public string message;
}
    
[KesError(0,"UnknownError", "不明なエラーが発生しました。")]
//lex error
[KesError(1,"AppearedInvalidToken", "字句解析エラー。不明なトークンが見つかりました。")]
[KesError(2,"StringLiteralNotClosed", "字句解析エラー。文字列リテラルが閉じられていません。")]
[KesError(3,"LineBreakDetectedInTheStringLiteral", "字句解析エラー。文字列の途中で改行が検出されました。複数行に渡って文字列を記述したい場合は、+演算子を用いてください。")]
[KesError(4,"FailedToReadNumberLiteral", "字句解析エラー。数値リテラルの解析に失敗しました。")]
[KesError(5,"CommentBlockNotClosed", "字句解析エラー。コメントブロックが閉じられていません。")]
//parse error
[KesError(100,"SemicolonExpected", "構文解析エラー。;が必要です。")]
[KesError(101,"TopLevelStatementNotAllowed", "構文解析エラー。トップレベルにはステートメントを記述できません。")]
[KesError(102,"IdentifierExpected", "構文解析エラー。識別子がありません。")]
[KesError(103,"TypeExpected", "構文解析エラー。型がありません。")]
[KesError(104,"LeftBraceExpected", "構文解析エラー。{が必要です。")]
[KesError(105,"RightBraceExpected", "構文解析エラー。}が必要です。")]
[KesError(106,"InvalidDeclaration", "構文解析エラー。宣言が不正です。")]
[KesError(107,"LeftParenthesisExpected", "構文解析エラー。(が必要です。")]
[KesError(108,"RightParenthesisExpected", "構文解析エラー。)が必要です。")]
[KesError(109,"CoronExpected", "構文解析エラー。:が必要です。")]
[KesError(110,"ExpressionExpected", "構文解析エラー。式が必要です。")]
[KesError(111,"ImplicitlyTypeMustBeInitialized", "構文解析エラー。暗黙的に型推論された変数は初期化子を持つ必要があります。")]
[KesError(112,"WhileKeywordExpected", "構文解析エラー。whileキーワードが必要です。")]
[KesError(113,"ForKeywordExpected", "構文解析エラー。forキーワードが必要です。")]
[KesError(114,"ForInitializerExpectVarDeclOrExpr", "構文解析エラー。for文の初期化子には変数宣言か式が必要です。")]
[KesError(115,"IfKeywordExpected", "構文解析エラー。ifキーワードが必要です。")]
[KesError(116, "ReturnKeywordExpected", "構文解析エラー。returnキーワードが必要です。")]
[KesError(117,"BreakKeywordExpected", "構文解析エラー。breakキーワードが必要です。")]
[KesError(118,"ContinueKeywordExpected", "構文解析エラー。continueキーワードが必要です。")]
[KesError(119,"InvalidExprTerm", "構文解析エラー。不正な{0}が検出されました。")]
[KesError(120,"LeftOfAssignMustBeLeftValue", "構文解析エラー。代入の左辺は左辺値である必要があります。")]
[KesError(121, "CommaExpected", "構文解析エラー。,が必要です。")]
[KesError(122,"NewKeywordExpected", "構文解析エラー。newキーワードが必要です。")]
// semantic error
[KesError(123, "TypeAlreadyDefined", "意味解析エラー。型{0}はすでに定義されています。")]
[KesError(124, "TypeNotFound", "意味解析エラー。型{0}が見つかりません。")]
[KesError(125, "InvalidTypeInCondition", "意味解析エラー。条件式にはbool型の式を指定してください。")]
[KesError(126, "InvalidTypeOfReturn", "意味解析エラー。戻り値の型が一致しません。")]
[KesError(127, "CannotConvertType", "意味解析エラー。型{0}から型{1}への変換はできません。")]
[KesError(128,"InvalidTypeOfOperator", "意味解析エラー。{0}演算子は{1}型には適用できません。")]
[KesError(129, "VariableAlreadyDefined", "意味解析エラー。変数{0}はすでに定義されています。")]
[KesError(130,"UsedUndefinedSymbol", "意味解析エラー。未定義のシンボル{0}が使用されています。")]
[KesError(131, "FunctionNotFound", "意味解析エラー。関数{0}が見つかりません。")]
[KesError(132, "FieldNotFound", "意味解析エラー。クラス{0}にフィールド{1}が見つかりません。")]
[KesError(133, "MethodNotFound", "意味解析エラー。クラス{0}にメソッド{1}が見つかりません。")]
[KesError(134, "CannotAssignToImmutableVariable", "意味解析エラー。左辺は定数です。")]
[KesError(135, "CannotCreateObjectAsPrimitiveType", "意味解析エラー。値型はnewできません。")]
[KesError(136, "CircularInheritance", "意味解析エラー。クラス{0}は循環継承しています。")]
[KesError(137,"InvalidMemberAccessFormat","意味解析エラー。メンバーアクセス式の中に、変数、関数以外のものが含まれています。")]
[KesError(138, "MemberNotFound", "意味解析エラー。クラス{0}にメンバ{1}が見つかりません。")]
[KesError(139, "FieldUsedAsMethod", "意味解析エラー。フィールド変数が関数として呼ばれました。")]
[KesError(140, "InvalidTypeOfOperatorBinary", "意味解析エラー。{0}演算子は{1}型と{2}型には適用できません。")]
[KesError(200, "CannotFindEntryPoint","意味解析エラー。エントリーポイントが見つかりません。グローバル空間にmain関数を定義してください。")]
// codegen error
[KesError(201, "InvalidVarScope", "コード生成エラー。変数{0}のスコープが不正です。")]
// runtime error
[KesError(1000, "InvalidAddress", "ランタイムエラー。不正なアドレスにアクセスしました。")]
public static partial class ErrorCode
{
    //エラーコードの実装はErrorCode.g.csにて、属性を元に自動生成されます。
}

public class CompileErrorException(KesErrorType errorInfo, params object[] data)
    : Exception(errorInfo.message)
{
    public KesErrorType ErrorInfo { get; } = errorInfo;
    public object[] OptionData { get; } = data;

}

public class KesRuntimeErrorException(KesErrorType errorInfo, params object[] data)
    : Exception(errorInfo.message)
{
    public KesErrorType ErrorInfo { get; } = errorInfo;
    public object[] OptionData { get; } = data;

}