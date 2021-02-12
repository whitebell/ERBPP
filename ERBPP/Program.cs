﻿using System;
using System.IO;
using System.Text;
using Whitebell.Library.Utils;
using System.Collections.Generic;

namespace ERBPP
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
#if DEBUG
            using var fs = File.Open("D:\\Game\\eratoho\\EVENT_K14\\EVENT_K14.ERB", FileMode.Open, FileAccess.Read);
            using var sr = new StreamReader(fs, Encoding.UTF8);
            using var fw = File.Open("D:\\Game\\eratoho\\EVENT_K14\\EVENT_K14.PP.ERB", FileMode.Create, FileAccess.Write);
            using var sw = new StreamWriter(fw, Encoding.UTF8) { AutoFlush = true };
#elif RELEASE
            var sr = new StreamReader(Console.OpenStandardInput(), Encoding.UTF8);
            var sw = new StreamWriter(Console.OpenStandardOutput(), Encoding.UTF8) { AutoFlush = true };
#endif

            string l;
            var curIndentLv = 0;
            var prevType = LineType.Unknown;
            var regionStack = new Stack<int>();
            while ((l = sr.ReadLine()?.TrimStart()) is not null)
            {
            REDO:
                if (l is null)
                    break; // goto REDO でここに戻った場合 l が null でありうる

                var t = new PseudoLexer(l).GetToken();

                switch (t.Type)
                {
                    case LineType.Blank:
                        sw.WriteLine();
                        break;

                    case LineType.FunctionDefinition:
                        if (curIndentLv != 0)
                            throw new FormatException("indented func def."); // 関数定義行でインデントされているのはおかしいので例外投げる

                        sw.WriteLine(l);
                        break;

                    case LineType.EndRegionComment:
                        {
                            if (!regionStack.TryPop(out var res))
                                throw new FormatException("region/endregion stack err.");
                            else
                                sw.Write(new string('\t', res));
                        }
                        sw.WriteLine(l);
                        break;

                    case LineType.If:
                    case LineType.Repeat:
                    case LineType.While:
                    case LineType.For:
                    case LineType.Do:
                    case LineType.TryCallList:
                    case LineType.TryCCall:
                    case LineType.SelectCase:
                    case LineType.PrintData:
                        sw.Write(new string('\t', curIndentLv++));
                        sw.WriteLine(l);
                        break;
                    case LineType.ElseIf:
                    case LineType.Else:
                    case LineType.Catch:
                        sw.Write(new string('\t', curIndentLv - 1));
                        sw.WriteLine(l);
                        break;
                    case LineType.Endif:
                    case LineType.Rend:
                    case LineType.Wend:
                    case LineType.Next:
                    case LineType.Loop:
                    case LineType.EndFunc:
                    case LineType.EndCatch:
                    case LineType.EndData:
                        sw.Write(new string('\t', --curIndentLv));
                        sw.WriteLine(l);
                        break;
                    case LineType.Case:
                    case LineType.CaseElse:
                        if (prevType == LineType.SelectCase)
                            sw.Write(new string('\t', curIndentLv++)); // LineType.CaseElseでここにくるのは正気じゃないと思うが、実例があるのでしょうがない。eratohoJ+ REVMODE.ERB
                        else
                            sw.Write(new string('\t', curIndentLv - 1));
                        sw.WriteLine(l);
                        break;
                    case LineType.EndSelect:
                        curIndentLv -= prevType == LineType.SelectCase ? 1 : 2; // eraTW ERB/MOVEMENTS/物件関連/JOB_MANAGE.ERB CASE,CASEELSEがない空のSELECTCASE～ENDSELECT。正気じゃない。
                        sw.Write(new string('\t', curIndentLv));
                        sw.WriteLine(l);
                        break;

                    case LineType.Sif:
                        {
                            sw.Write(new string('\t', curIndentLv));
                            sw.WriteLine(l);
                            l = sr.ReadLine()?.TrimStart();
                            if (l is null)
                                throw new FormatException("last line sif");

                            // eratohoJ+ COMF140.ERB SIFの次にコメント行。しぬべき。
                            var lst = new List<string>();
                            do
                            {
                                lst.Add(l);
                                t = new PseudoLexer(l).GetToken();
                                if (t.Type != LineType.Comment)
                                    break;
                            } while ((l = sr.ReadLine()?.TrimStart()) is not null);
                            foreach (var e in lst)
                            {
                                sw.Write(new string('\t', curIndentLv + 1));
                                sw.WriteLine(e);
                            }
                        }
                        break;

                    case LineType.Comment:
                    case LineType.StartRegionComment:
                        {
                            var lst = new List<(LineType, string)> { (t.Type, l) };
                            string nl;
                            LineType nextType = LineType.Unknown;
                            while ((nl = sr.ReadLine()?.TrimStart()) is not null)
                            {
                                var nt = new PseudoLexer(nl).GetToken();
                                switch (nt.Type)
                                {
                                    case LineType.Blank:
                                    case LineType.Comment:
                                    case LineType.StartRegionComment:
                                    case LineType.EndRegionComment:
                                        lst.Add((nt.Type, nl));
                                        break;
                                    default:
                                        nextType = nt.Type;
                                        goto BREAK;
                                }
                            }
                        BREAK:
                            switch (nextType)
                            {
                                case LineType.ElseIf:
                                case LineType.Else:
                                case LineType.Case:
                                case LineType.CaseElse:
                                case LineType.EndSelect:
                                    foreach (var e in lst)
                                    {
                                        if (e.Item1 == LineType.Blank)
                                        {
                                            sw.WriteLine();
                                        }
                                        else
                                        {
                                            var indentLv = (nextType == LineType.Case || nextType == LineType.CaseElse) && prevType == LineType.SelectCase ? curIndentLv : Math.Max(0, curIndentLv - 1);
                                            switch (e.Item1)
                                            {
                                                case LineType.StartRegionComment:
                                                    regionStack.Push(indentLv);
                                                    sw.Write(new string('\t', indentLv));
                                                    sw.WriteLine(e.Item2);
                                                    break;
                                                case LineType.EndRegionComment:
                                                    {
                                                        if (!regionStack.TryPop(out var res))
                                                            throw new FormatException("region/endregion stack err.");
                                                        else
                                                            sw.Write(new string('\t', res));
                                                    }
                                                    sw.WriteLine(e.Item2);
                                                    break;
                                                default:
                                                    sw.Write(new string('\t', indentLv));
                                                    sw.WriteLine(e.Item2);
                                                    break;
                                            }
                                        }
                                    }
                                    break;
                                default:
                                    foreach (var e in lst)
                                    {
                                        switch (e.Item1)
                                        {
                                            case LineType.Blank:
                                                sw.WriteLine();
                                                break;
                                            case LineType.StartRegionComment:
                                                regionStack.Push(curIndentLv);
                                                sw.Write(new string('\t', curIndentLv));
                                                sw.WriteLine(e.Item2);
                                                break;
                                            case LineType.EndRegionComment:
                                                {
                                                    if (!regionStack.TryPop(out var res))
                                                        throw new FormatException("region/endregion stack err.");
                                                    else
                                                        sw.Write(new string('\t', res));
                                                }
                                                sw.WriteLine(e.Item2);
                                                break;
                                            default:
                                                sw.Write(new string('\t', curIndentLv));
                                                sw.WriteLine(e.Item2);
                                                break;
                                        }
                                    }
                                    break;
                            }
                            l = nl;
                        }
                        goto REDO;

                    default:
                        sw.Write(new string('\t', curIndentLv));
                        sw.WriteLine(l);
                        break;
                }

                if (t.Type != LineType.Blank && t.Type != LineType.Comment && t.Type != LineType.StartRegionComment && t.Type != LineType.EndRegionComment)
                    prevType = t.Type;
                sw.Flush();
            }

            if (regionStack.Count != 0)
                throw new FormatException($"region/endregion stack err. c={regionStack.Count}");
        }
    }

    public class PseudoLexer
    {
        private readonly StringStream ss;

        private static readonly HashSet<string> variable = new();

        public PseudoLexer(string s) => ss = new StringStream(s.TrimEnd('\r', '\n'));

        private void SkipSpace()
        {
            while (!ss.EndOfStream && Char.IsWhiteSpace(ss.Current))
                ss.NextChar(out _);
        }

        //private void Skip(int i)
        //{
        //    var s = 0;
        //    while (!ss.EndOfStream && s++ < i)
        //        ss.NextChar(out _);
        //}

        private void Consume(char c)
        {
            if (!ss.EndOfStream && ss.Current == c)
                ss.NextChar(out _);
        }

        private string GetIdent()
        {
            var sb = new StringBuilder();
            if (!ss.EndOfStream && IsIdentCharFirst(ss.Current) && !IsVariableSeparator(ss.Current))
            {
                ss.NextChar(out var c);
                sb.Append(c);
            }
            while (!ss.EndOfStream && IsIdentChar(ss.Current) && !IsVariableSeparator(ss.Current))
            {
                ss.NextChar(out var c);
                sb.Append(c);
            }
            return sb.ToString();
        }

        public Token GetToken()
        {
            SkipSpace();

            if (ss.EndOfStream)
            {
                return new Token { Type = LineType.Blank };
            }
            else if (IsCommentStart(ss.Current))
            {
                Consume(';');
                SkipSpace();
                if (ss.Current == '#')
                {
                    Consume('#');
                    SkipSpace();
                }

                return GetIdent().ToLower() switch
                {
                    "region"    => new Token { Type = LineType.StartRegionComment },
                    "endregion" => new Token { Type = LineType.EndRegionComment },
                    _           => new Token { Type = LineType.Comment },
                };
            }
            else if (IsFunctionStart(ss.Current))
            {
                variable.Clear();
                return new Token { Type = LineType.FunctionDefinition };
            }
            else if (IsAttributeStart(ss.Current))
            {
                Consume('#');
                switch (GetIdent().ToUpper())
                {
                    case "FUNCTION":
                    case "LATER":
                    case "PRI":
                        return new Token { Type = LineType.Attribute };
                    case "DIM":
                    case "DIMS":
                        {
                            SkipSpace();
                            var v = GetIdent();
                            if (v.Equals("dynamic", StringComparison.OrdinalIgnoreCase))
                            {
                                SkipSpace();
                                v = GetIdent();
                            }
                            v = v.ToUpper(); //全部大文字にして登録する。eraTWアリス口上 日常系コマンドで大文字小文字の混乱がある。
                            if (!variable.Contains(v))
                                variable.Add(v);
                            return new Token { Type = LineType.VariableDefinition };
                        }
                    default:
                        return new Token { Type = LineType.Unknown };
                }
            }
            else if (IsLabelStart(ss.Current))
            {
                return new Token { Type = LineType.Label };
            }
            else if (IsSpBlockStart(ss.Current))
            {
                Consume('[');
                var ident = GetIdent();
                switch (ident.ToUpper())
                {
                    case "SKIPSTART":
                    case "SKIPEND":
                    case "IF":
                    case "ELSEIF":
                    case "ELSE":
                    case "ENDIF":
                    case "IF_DEBUG":
                    case "IF_NDEBUG":
                        return new Token { Type = LineType.SpBlock };
                    default:
                        throw new FormatException($"unknown spblock: {ident}");
                }
            }
            else if (IsIncr(ss.Current))
            {
                if (IsIncr(ss.Peek(1)))
                {
                    Consume('+');
                    Consume('+');
                    SkipSpace();
                    var t = GetToken();
                    if (t.Type == LineType.Variable)
                        return new Token { Type = LineType.Variable };
                    else
                        throw new FormatException("incr op. + nonvariable");
                }
                throw new FormatException($"can't parse. {ss.RawString}");
            }
            else if (IsDecr(ss.Current))
            {
                if (IsDecr(ss.Peek(1)))
                {
                    Consume('-');
                    Consume('-');
                    SkipSpace();
                    var t = GetToken();
                    if (t.Type == LineType.Variable)
                        return new Token { Type = LineType.Variable };
                    else
                        throw new FormatException("incr op. + nonvariable");
                }
                throw new FormatException($"can't parse. {ss.RawString}");
            }
            else
            {
                var ident = GetIdent(); // 小文字のユーザ定義変数があった。ここで大文字にしてしまうとdefaultのUDVを見るところで誤って見落としてしまう。辞書に入れる時点で全部大文字にしてしまうか。
                switch (ident.ToUpper())
                {
                    case "SIF":
                        return new Token { Type = LineType.Sif };

                    case "IF":
                        return new Token { Type = LineType.If };
                    case "ELSEIF":
                        return new Token { Type = LineType.ElseIf };
                    case "ELSE":
                        return new Token { Type = LineType.Else };
                    case "ENDIF":
                        return new Token { Type = LineType.Endif };

                    case "REPEAT":
                        return new Token { Type = LineType.Repeat };
                    case "REND":
                        return new Token { Type = LineType.Rend };

                    case "SELECTCASE":
                        return new Token { Type = LineType.SelectCase };
                    case "CASE":
                        return new Token { Type = LineType.Case };
                    case "CASEELSE":
                        return new Token { Type = LineType.CaseElse };
                    case "ENDSELECT":
                        return new Token { Type = LineType.EndSelect };

                    case "FOR":
                        return new Token { Type = LineType.For };
                    case "NEXT":
                        return new Token { Type = LineType.Next };

                    case "WHILE":
                        return new Token { Type = LineType.While };
                    case "WEND":
                        return new Token { Type = LineType.Wend };

                    case "DO":
                        return new Token { Type = LineType.Do };
                    case "LOOP":
                        return new Token { Type = LineType.Loop };

                    case "BREAK":
                        return new Token { Type = LineType.Break };

                    case "CONTINUE":
                        return new Token { Type = LineType.Continue };

                    case "TRYCALLLIST":
                        return new Token { Type = LineType.TryCallList };
                    case "FUNC":
                        return new Token { Type = LineType.FuncList };
                    case "ENDFUNC":
                        return new Token { Type = LineType.EndFunc };

                    case "TRYCCALL":
                    case "TRYCCALLFORM":
                        return new Token { Type = LineType.TryCCall };
                    case "CATCH":
                        return new Token { Type = LineType.Catch };
                    case "ENDCATCH":
                        return new Token { Type = LineType.EndCatch };

                    case "BEGIN":
                        return new Token { Type = LineType.Begin };

                    case "RESTART":
                        return new Token { Type = LineType.Restart };

                    case "CALL":
                    case "CALLF":
                    case "CALLFORM":
                    case "CALLFORMF":
                    case "CALLTRAIN": //本来分けたほうがいいと思うけど、インデント修正するだけならいらない
                    case "TRYCALL":
                    case "TRYCALLFORM":
                        return new Token { Type = LineType.Call };

                    case "JUMP":
                    case "JUMPFORM":
                        return new Token { Type = LineType.Jump };

                    case "GOTO":
                        return new Token { Type = LineType.Goto };

                    case "RETURN":
                    case "RETURNF":
                    case "RETURNFORM":
                        return new Token { Type = LineType.Return };

                    case "THROW":
                        return new Token { Type = LineType.Throw };

                    case "PRINTDATA":
                    case "PRINTDATAD":
                    case "PRINTDATADL":
                    case "PRINTDATADW":
                    case "PRINTDATAL":
                    case "PRINTDATAW":
                        return new Token { Type = LineType.PrintData };
                    case "DATA":
                    case "DATAFORM":
                        return new Token { Type = LineType.Data };
                    case "ENDDATA":
                        return new Token { Type = LineType.EndData };

                    case "ADDCHARA":
                    case "ADDCOPYCHARA":
                    case "ADDSPCHARA":
                    case "ADDVOIDCHARA":
                    case "ALIGNMENT":
                    case "ARRAYCOPY":
                    case "ARRAYREMOVE":
                    case "ARRAYSHIFT":
                    case "ARRAYSORT":
                    case "BAR":
                    case "CHKCHARADATA":
                    case "CHKDATA":
                    case "CLEARBIT":
                    case "CLEARLINE":
                    case "CURRENTREDRAW":
                    case "CSVABL":
                    case "CSVCALLNAME":
                    case "CSVCSTR":
                    case "CSVNAME":
                    case "CUSTOMDRAWLINE":
                    case "CVARSET":
                    case "DEBUGCLEAR":
                    case "DEBUGPRINT":
                    case "DEBUGPRINTFORML":
                    case "DEBUGPRINTL":
                    case "DELCHARA":
                    case "DOTRAIN":
                    case "DRAWLINE":
                    case "DRAWLINEFORM":
                    case "DUMPRAND":
                    case "EXISTCSV":
                    case "FINDCHARA":
                    case "FONTBOLD":
                    case "FONTITALIC":
                    case "FONTREGULAR":
                    case "FONTSTYLE":
                    case "FORCEWAIT":
                    case "GETBIT":
                    case "GETCHARA":
                    case "GETDEFCOLOR":
                    case "GETFONT":
                    case "GETPALAMLV":
                    case "GETTIME":
                    case "HTML_PRINT":
                    case "INITRAND":
                    case "INPUT":
                    case "INPUTS":
                    case "INVERTBIT":
                    case "LIMIT":
                    case "LOADCHARA":
                    case "LOADDATA":
                    case "LOADGAME":
                    case "LOADGLOBAL":
                    case "ONEINPUT":
                    case "ONEINPUTS":
                    case "POWER":
                    case "PRINT":
                    case "PRINTBUTTON":
                    case "PRINTC":
                    case "PRINTCD":
                    case "PRINTD":
                    case "PRINTDL":
                    case "PRINTDW":
                    case "PRINTL":
                    case "PRINTLC":
                    case "PRINTS":
                    case "PRINTSD":
                    case "PRINTSINGLEFORM":
                    case "PRINTSINGLEFORMS":
                    case "PRINTSL":
                    case "PRINTSW":
                    case "PRINTV":
                    case "PRINTVL":
                    case "PRINTW":
                    case "PRINTFORM":
                    case "PRINTFORMC":
                    case "PRINTFORMCD":
                    case "PRINTFORMD":
                    case "PRINTFORMDL":
                    case "PRINTFORMKW":
                    case "PRINTFORML":
                    case "PRINTFORMLC":
                    case "PRINTFORMDW":
                    case "PRINTFORMS":
                    case "PRINTFORMSL":
                    case "PRINTFORMW":
                    case "PRINTPLAIN":
                    case "PRINTPLAINFORM":
                    case "PRINT_ABL":
                    case "PRINT_EXP":
                    case "PRINT_ITEM":
                    case "PRINT_MARK":
                    case "PRINT_PALAM":
                    case "PRINT_SHOPITEM":
                    case "PRINT_TALENT":
                    case "PUTFORM":
                    case "RANDOMIZE":
                    case "REDRAW":
                    case "REPLACE":
                    case "RESET_STAIN":
                    case "RESETBGCOLOR":
                    case "RESETCOLOR":
                    case "RESETDATA":
                    case "REUSELASTLINE":
                    case "SAVECHARA":
                    case "SAVEGAME":
                    case "SAVEGLOBAL":
                    case "SETBGCOLOR":
                    case "SETCOLOR":
                    case "SETFONT":
                    case "SKIPDISP":
                    case "SORTCHARA":
                    case "SPLIT":
                    case "STRFIND":
                    case "STRLENFORM":
                    case "STRLENS":
                    case "SUBSTRING":
                    case "SUBSTRINGU":
                    case "SWAP":
                    case "SWAPCHARA":
                    case "TIMES":
                    case "TINPUT":
                    case "TWAIT":
                    case "UPCHECK":
                    case "QUIT":
                    case "SETBIT":
                    case "VARSET":
                    case "VARSIZE":
                    case "WAIT":
                        return new Token { Type = LineType.BuiltinFunction };

                    case "MASTER":
                    case "PLAYER":
                    case "TARGET":
                    case "ASSI":
                    case "ABL":
                    case "ARG":
                    case "ARGS":
                    case "ASSIPLAY":
                    case "BASE":
                    case "MAXBASE":
                    case "BOUGHT":
                    case "CALLNAME":
                    case "CDFLAG":
                    case "CDOWN":
                    case "CFLAG":
                    case "COUNT":
                    case "CSTR":
                    case "CUP":
                    case "DAY":
                    case "DOWN":
                    case "DOWNBASE":
                    case "EJAC":
                    case "EQUIP":
                    case "EX":
                    case "EXP":
                    case "EXPLV":
                    case "FLAG":
                    case "GLOBAL":
                    case "GLOBALS":
                    case "GOTJUEL":
                    case "ISASSI":
                    case "ITEM":
                    case "ITEMSALES":
                    case "JUEL":
                    case "LOCAL":
                    case "LOCALS":
                    case "LOSEBASE":
                    case "MARK":
                    case "MASTERNAME":
                    case "MONEY":
                    case "NAME":
                    case "NEXTCOM":
                    case "NICKNAME":
                    case "NO":
                    case "NOITEM":
                    case "NOWEX":
                    case "PALAM":
                    case "PALAMLV":
                    case "PBAND":
                    case "PREVCOM":
                    case "RANDDATA":
                    case "RELATION":
                    case "RESULT":
                    case "RESULTS":
                    case "SAVEDATA_TEXT":
                    case "SAVESTR":
                    case "SELECTCOM":
                    case "SOURCE":
                    case "STAIN":
                    case "STR":
                    case "TALENT":
                    case "TCVAR":
                    case "TEQUIP":
                    case "TFLAG":
                    case "TIME":
                    case "TSTR":
                    case "UP":
                    case "WINDOW_TITLE":
                    case "A":
                    case "B":
                    case "C":
                    case "D":
                    case "E":
                    case "F":
                    case "G":
                    case "H":
                    case "I":
                    case "J":
                    case "K":
                    case "L":
                    case "M":
                    case "N":
                    case "O":
                    case "P":
                    case "Q":
                    case "R":
                    case "S":
                    case "T":
                    case "U":
                    case "V":
                    case "W":
                    case "X":
                    case "Y":
                    case "Z":
                    case "DA":
                    case "DB":
                    case "DC":
                    case "DD":
                    case "DE":
                    case "DITEMTYPE":
                    case "TA":
                    case "TB":
                        return new Token { Type = LineType.Variable };

                    default:
                        if (variable.Contains(ident.ToUpper()))
                            return new Token { Type = LineType.Variable };
                        throw new FormatException($"unknown ident name ({(String.IsNullOrWhiteSpace(ident) ? ss.RawString : ident)})");
                        //return new Token { Type = LineType.ErhUserDefVariable }; // ERHで定義されたグローバルなUDVだとここに来る
                        //現状では上のcaseで判定していない関数/変数があるのでそれもここにきてしまう。
                }
            }
        }

        private static bool IsCommentStart(char c) => c == ';';
        private static bool IsFunctionStart(char c) => c == '@';
        private static bool IsAttributeStart(char c) => c == '#';
        private static bool IsVariableSeparator(char c) => c == ':';
        private static bool IsLabelStart(char c) => c == '$';
        private static bool IsIdentCharFirst(char c) => c == '_' || Char.IsLetter(c);
        private static bool IsIdentChar(char c) => c == '_' || Char.IsLetterOrDigit(c);
        private static bool IsIncr(char c) => c == '+';
        private static bool IsDecr(char c) => c == '-';
        private static bool IsSpBlockStart(char c) => c == '[';
        private static bool IsSpBlockEnd(char c) => c == ']';
        //private static bool IsIdentStart(char c) => Char.IsLetter(c);

        //private Token Ident() => throw new NotImplementedException();
    }

    public class Token
    {
        public string Value { get; init; } = "";
        public LineType Type { get; init; } = LineType.Unknown;
    }

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
        FuncList,
        EndFunc,

        TryCCall,
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
