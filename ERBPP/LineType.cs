namespace ERBPP
{
    public enum LineType
    {
        /// <summary>
        /// 空行
        /// </summary>
        Blank,

        /// <summary>
        /// コメント行
        /// </summary>
        Comment,

        /// <summary>
        /// リージョンコメント開始
        /// </summary>
        StartRegionComment,
        /// <summary>
        /// リージョンコメント終了
        /// </summary>
        EndRegionComment,

        /// <summary>
        /// 行連結開始 '{'
        /// </summary>
        StartConcat,
        /// <summary>
        /// 行連結終了 '}'
        /// </summary>
        EndConcat,

        /// <summary>
        /// 関数定義
        /// </summary>
        FunctionDefinition,

        /// <summary>
        /// 関数属性 PRI, LATER, etc...
        /// </summary>
        Attribute,

        /// <summary>
        /// ラベル行
        /// </summary>
        Label,

        /// <summary>
        /// 特殊ブロック [SKIPSTART][SKIPEND]など https://ja.osdn.net/projects/emuera/wiki/exfunc#h3-.E7.89.B9.E6.AE.8A.E3.81.AA.E3.83.96.E3.83.AD.E3.83.83.E3.82.AF.E3.82.92.E8.A1.A8.E3.81.99.E8.A1.8C
        /// </summary>
        SpBlock,

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

        /// <summary>
        /// 変数
        /// </summary>
        Variable,

        /// <summary>
        /// ERHで定義されたグローバルなユーザ定義変数
        /// </summary>
        ErhUserDefVariable,

        Unknown,
    }
}
