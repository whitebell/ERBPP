namespace ERBPP
{
    public enum LineType
    {
        Unknown = 0,

        /// <summary>
        /// 空行
        /// </summary>
        Blank,

        /// <summary>
        /// コメント行 ;COMMENT
        /// </summary>
        Comment,

        /// <summary>
        /// リージョンコメント開始 ;region, ;#region
        /// </summary>
        StartRegionComment,
        /// <summary>
        /// リージョンコメント終了 ;endregion, ;#endregion
        /// </summary>
        EndRegionComment,

        /// <summary>
        /// 行連結開始 {
        /// </summary>
        StartConcat,
        /// <summary>
        /// 行連結終了 }
        /// </summary>
        EndConcat,

        /// <summary>
        /// 関数定義 @FUNCTION
        /// </summary>
        FunctionDefinition,

        /// <summary>
        /// 関数属性 #PRI, #LATER, etc...
        /// </summary>
        Attribute,

        /// <summary>
        /// ラベル行 $LABEL
        /// </summary>
        Label,

        /// <summary>
        /// <para>特殊ブロック [SKIPSTART][SKIPEND]など</para>
        /// <para><see href="https://ja.osdn.net/projects/emuera/wiki/exfunc#h3-.E7.89.B9.E6.AE.8A.E3.81.AA.E3.83.96.E3.83.AD.E3.83.83.E3.82.AF.E3.82.92.E8.A1.A8.E3.81.99.E8.A1.8C">exfunc - Emuera - emulator of eramaker Wiki - Emuera - emulator of eramaker - OSDN</see></para>
        /// </summary>
        SpBlock,

        /// <summary>
        /// 変数定義 #DIM, #DIMS
        /// </summary>
        VariableDefinition,

        Sif,

        If,
        Else,
        ElseIf,
        Endif,

        Repeat,
        Rend,

        SelectCase,
        Case,
        CaseElse,
        EndSelect,

        For,
        Next,

        While,
        Wend,

        Do,
        Loop,

        Break,

        Continue,

        TryCallList,
        TryGotoList,
        TryJumpList,
        Func,
        EndFunc,

        TryCCall,
        TryCGoto,
        TryCJump,
        Catch,
        EndCatch,

        Call,

        Jump,

        Goto,

        Begin,

        Restart,

        Return,

        Throw,

        PrintData,
        StrData,
        DataList,
        EndList,
        Data,
        EndData,

        /// <summary>
        /// 組み込み関数
        /// </summary>
        BuiltinFunction,

        // ERHファイルは読まない（使用方法的にERHの読みようがない）ので
        // 確実に変数であると判断できる組み込み変数・関数ローカルのユーザ定義変数と
        // ERHで定義されたグローバルなユーザ定義変数（と推定されるもの）を分けておく

        /// <summary>
        /// 組み込み変数、関数ローカルのユーザ定義変数
        /// </summary>
        Variable,

        /// <summary>
        /// ERHで定義されたグローバルなユーザ定義変数（と推定されるもの）
        /// </summary>
        ErhUserDefVariable,
    }
}
