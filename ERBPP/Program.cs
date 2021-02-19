using System;
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
            //while ((l = sr.ReadLine()?.TrimStart()) is not null)
            while (!sr.EndOfStream)
            {
                l = sr.ReadLine()!.TrimStart(); // because !sr.EndOfStream, sr.ReadLine() returns string.
            READLINE_REDO:
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
                    case LineType.TryGotoList:
                    case LineType.TryJumpList:
                    case LineType.TryCCall:
                    case LineType.TryCGoto:
                    case LineType.TryCJump:
                    case LineType.SelectCase:
                    case LineType.PrintData:
                    case LineType.StrData:
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
                    case LineType.EndList:
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
                    case LineType.DataList:
                        sw.Write(new string('\t', curIndentLv++));
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

                            // eratohoJ+ COMF140.ERB SIFの次にコメント行。しぬべき。
                            var lst = new List<string>();
                            while (!sr.EndOfStream)
                            {
                                l = sr.ReadLine()!.TrimStart(); //because !sr.EndOfStream, sr.ReadLine() returns string.
                                lst.Add(l);
                                t = new PseudoLexer(l).GetToken();
                                if (t.Type != LineType.Comment && t.Type != LineType.Blank && t.Type != LineType.StartRegionComment && t.Type != LineType.EndRegionComment)
                                    break;
                            }
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
                            string? nl;
                            var nextType = LineType.Unknown;
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
                                        goto READ_COMMENT_END;
                                }
                            }
                        READ_COMMENT_END:
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
                        if (l is null)
                            goto READLINE_BREAK;

                        goto READLINE_REDO;

                    case LineType.Unknown:
                        throw new FormatException($"unknown line type. ({l})");

                    default:
                        sw.Write(new string('\t', curIndentLv));
                        sw.WriteLine(l);
                        break;
                }

                if (t.Type != LineType.Blank && t.Type != LineType.Comment && t.Type != LineType.StartRegionComment && t.Type != LineType.EndRegionComment)
                    prevType = t.Type;
                sw.Flush();
            }
        READLINE_BREAK:

            if (regionStack.Count != 0)
                throw new FormatException($"region/endregion stack err. c={regionStack.Count}");
        }
    }

    public class PseudoLexer
    {
        private readonly StringStream ss;

        private static readonly HashSet<string> functionLocalUdv = new();
        private static readonly HashSet<string> erhGlobalUdv = new();

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
                functionLocalUdv.Clear();
                return new Token { Type = LineType.FunctionDefinition };
            }
            else if (IsAttributeStart(ss.Current))
            {
                Consume('#');
                var ident = GetIdent();
                switch (ident.ToUpper())
                {
                    case "ONLY":
                    case "FUNCTION":
                    case "FUNCTIONS":
                    case "LOCALSIZE":
                    case "LOCALSSIZE":
                    case "LATER":
                    case "PRI":
                        return new Token { Type = LineType.Attribute };
                    case "DIM":
                    case "DIMS":
                        {
                            SkipSpace();
                            //全部大文字にして登録する。eraTWアリス口上 日常系コマンドで大文字小文字の混乱がある。
                            var v = GetIdent().ToUpper();
                            // 動的・参照型・定数
                            if (v == "DYNAMIC" || v == "REF" || v == "CONST")
                            {
                                SkipSpace();
                                v = GetIdent().ToUpper();
                            }
                            if (!functionLocalUdv.Contains(v))
                                functionLocalUdv.Add(v);
                            return new Token { Type = LineType.VariableDefinition };
                        }
                    default:
                        throw new FormatException($"unknown attribute. ({ident})");
                }
            }
            else if (IsLabelStart(ss.Current))
            {
                return new Token { Type = LineType.Label };
            }
            else if (IsSpBlockStart(ss.Current))
            {
                Consume('[');
                SkipSpace();
                var ident = GetIdent();
                SkipSpace();
                switch (ident.ToUpper())
                {
                    case "SKIPSTART":
                    case "SKIPEND":
                    //case "IF":
                    //case "ELSEIF":
                    case "ELSE":
                    case "ENDIF":
                    case "IF_DEBUG":
                    case "IF_NDEBUG":
                        return IsSpBlockEnd(ss.Current) ? new Token { Type = LineType.SpBlock } : throw new FormatException($"unknown spblock: {ident}");
                    case "IF":
                    case "ELSEIF":
                        SkipSpace();
                        GetIdent();
                        SkipSpace();
                        return IsSpBlockEnd(ss.Current) ? new Token { Type = LineType.SpBlock } : throw new FormatException($"unknown spblock: {ident}");
                    default:
                        throw new FormatException($"unknown spblock: {ident}");
                }
            }
            else if (IsIncr(ss.Current))
            {
                if (!IsIncr(ss.Peek(1)))
                    throw new FormatException($"can't parse. {ss.RawString}");

                Consume('+');
                Consume('+');
                SkipSpace();
                var t = GetToken();
                return t.Type == LineType.Variable ? new Token { Type = LineType.Variable } : throw new FormatException("incr op. + nonvariable");
            }
            else if (IsDecr(ss.Current))
            {
                if (!IsDecr(ss.Peek(1)))
                    throw new FormatException($"can't parse. {ss.RawString}");

                Consume('-');
                Consume('-');
                SkipSpace();
                var t = GetToken();
                return t.Type == LineType.Variable ? new Token { Type = LineType.Variable } : throw new FormatException("incr op. + nonvariable");
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
                    case "TRYGOTOLIST":
                        return new Token { Type = LineType.TryGotoList };
                    case "TRYJUMPLIST":
                        return new Token { Type = LineType.TryJumpList };
                    case "FUNC":
                        return new Token { Type = LineType.Func };
                    case "ENDFUNC":
                        return new Token { Type = LineType.EndFunc };

                    case "TRYCCALL":
                    case "TRYCCALLFORM":
                        return new Token { Type = LineType.TryCCall };
                    case "TRYCGOTO":
                    case "TRYCGOTOFORM":
                        return new Token { Type = LineType.TryCGoto };
                    case "TRYCJUMP":
                    case "TRYCJUMPFORM":
                        return new Token { Type = LineType.TryCJump };
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
                    case "TRYCALL":
                    case "TRYCALLFORM":
                        return new Token { Type = LineType.Call };

                    case "JUMP":
                    case "JUMPFORM":
                    case "TRYJUMP":
                    case "TRYJUMPFORM":
                        return new Token { Type = LineType.Jump };

                    case "GOTO":
                    case "GOTOFORM":
                    case "TRYGOTO":
                    case "TRYGOTOFORM":
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
                    case "PRINTDATAK":
                    case "PRINTDATAKL":
                    case "PRINTDATAKW":
                    case "PRINTDATAL":
                    case "PRINTDATAW":
                        return new Token { Type = LineType.PrintData };
                    case "STRDATA":
                        return new Token { Type = LineType.StrData };
                    case "DATALIST":
                        return new Token { Type = LineType.DataList };
                    case "ENDLIST":
                        return new Token { Type = LineType.EndList };
                    case "DATA":
                    case "DATAFORM":
                        return new Token { Type = LineType.Data };
                    case "ENDDATA":
                        return new Token { Type = LineType.EndData };

                    case "ABS":
                    case "ADDCHARA":
                    case "ADDCOPYCHARA":
                    case "ADDDEFCHARA":
                    case "ADDSPCHARA":
                    case "ADDVOIDCHARA":
                    case "ALIGNMENT":
                    case "ALLSAMES":
                    case "ARRAYCOPY":
                    case "ARRAYMSORT":
                    case "ARRAYREMOVE":
                    case "ARRAYSHIFT":
                    case "ARRAYSORT":
                    case "ASSERT":
                    case "AWAIT":
                    case "BAR":
                    case "BARSTR":
                    case "CALLEVENT":
                    case "CALLTRAIN":
                    case "CBGCLEAR":
                    case "CBGCLEARBUTTON":
                    case "CBGREMOVEBMAP":
                    case "CBGREMOVERANGE":
                    case "CBGSETBMAPG":
                    case "CBGSETBUTTONSPRITE":
                    case "CBGSETG":
                    case "CBGSETSPRITE":
                    case "CBRT":
                    case "CHARATU":
                    case "CHKCHARADATA":
                    case "CHKDATA":
                    case "CHKFONT":
                    case "CLEARBIT":
                    case "CLEARLINE":
                    case "CLEARTEXTBOX":
                    case "CLIENTHEIGHT ":
                    case "CLIENTWIDTH ":
                    case "CMATCH":
                    case "COLOR_FROMNAME":
                    case "COLOR_FROMRGB":
                    case "CONVERT":
                    case "COPYCHARA":
                    case "CSVABL":
                    case "CSVBASE":
                    case "CSVCALLNAME":
                    case "CSVCFLAG":
                    case "CSVCSTR":
                    case "CSVEQUIP":
                    case "CSVEXP":
                    case "CSVJUEL":
                    case "CSVJULE":
                    case "CSVMARK":
                    case "CSVMASTERNAME":
                    case "CSVNAME":
                    case "CSVNICKNAME":
                    case "CSVRELATION":
                    case "CSVTALENT":
                    case "CUPCHECK":
                    case "CURRENTALIGN":
                    case "CURRENTREDRAW":
                    case "CUSTOMDRAWLINE":
                    case "CVARSET":
                    case "DEBUGCLEAR":
                    case "DEBUGPRINT":
                    case "DEBUGPRINTFORM":
                    case "DEBUGPRINTFORML":
                    case "DEBUGPRINTL":
                    case "DELALLCHARA":
                    case "DELCHARA":
                    case "DELDATA":
                    case "DOTRAIN":
                    case "DRAWLINE":
                    case "DRAWLINEFORM":
                    case "DUMPRAND":
                    case "ENCODETOUNI":
                    case "ENDNOSKIP":
                    case "ESCAPE":
                    case "EXISTCSV":
                    case "EXPONENT":
                    case "FIND_CHARADATA":
                    case "FINDCHARA":
                    case "FINDELEMENT ":
                    case "FINDELEMENT":
                    case "FINDLASTCHARA":
                    case "FINDLASTELEMENT ":
                    case "FINDLASTELEMENT":
                    case "FONTBOLD":
                    case "FONTITALIC":
                    case "FONTREGULAR":
                    case "FONTSTYLE":
                    case "FORCEKANA":
                    case "FORCEWAIT":
                    case "GCLEAR":
                    case "GCREATE":
                    case "GCREATED":
                    case "GCREATEFROMFILE":
                    case "GDISPOSE":
                    case "GDRAWG":
                    case "GDRAWGWITHMASK":
                    case "GDRAWSPRITE":
                    case "GETBGCOLOR":
                    case "GETBIT":
                    case "GETCHARA":
                    case "GETCOLOR":
                    case "GETCONFIG":
                    case "GETCONFIGS":
                    case "GETDEFBGCOLOR":
                    case "GETDEFCOLOR":
                    case "GETEXPLV":
                    case "GETFOCUSCOLOR":
                    case "GETFONT":
                    case "GETKEY":
                    case "GETKEYTRIGGERED":
                    case "GETLINESTR":
                    case "GETMILLISECOND":
                    case "GETNUM":
                    case "GETPALAMLV":
                    case "GETSECOND":
                    case "GETSTYLE":
                    case "GETTIME":
                    case "GETTIMES":
                    case "GFILLRECTANGLE":
                    case "GGETCOLOR":
                    case "GHEIGHT":
                    case "GLOAD":
                    case "GROUPMATCH":
                    case "GSAVE":
                    case "GSETBRUSH":
                    case "GSETCOLOR":
                    case "GSETFONT":
                    case "GSETPEN":
                    case "GWIDTH":
                    case "HTML_ESCAPE":
                    case "HTML_GETPRINTEDSTR":
                    case "HTML_POPPRINTINGSTR":
                    case "HTML_PRINT":
                    case "HTML_TAGSPLIT":
                    case "HTML_TOPLAINTEXT":
                    case "INITRAND":
                    case "INPUT":
                    case "INPUTMOUSEKEY":
                    case "INPUTS":
                    case "INRANGE":
                    case "INRANGEARRAY":
                    case "INRANGECARRAY":
                    case "INVERTBIT":
                    case "ISACTIVE":
                    case "ISNUMERIC":
                    case "ISSKIP":
                    case "LIMIT":
                    case "LINEISEMPTY":
                    case "LOADCHARA":
                    case "LOADDATA":
                    case "LOADGAME":
                    case "LOADGLOBAL":
                    case "LOADTEXT":
                    case "LOG":
                    case "LOG10":
                    case "MATCH":
                    case "MAX":
                    case "MAXARRAY":
                    case "MAXCARRAY":
                    case "MESSKIP":
                    case "MIN":
                    case "MINARRAY":
                    case "MINCARRAY":
                    case "MONEYSTR":
                    case "MOUSESKIP":
                    case "MOUSEX":
                    case "MOUSEY":
                    case "NOSAMES":
                    case "NOSKIP":
                    case "ONEINPUT":
                    case "ONEINPUTS":
                    case "OUTPUTLOG":
                    case "PICKUPCHARA":
                    case "POWER":
                    case "PRINT":
                    case "PRINT_ABL":
                    case "PRINT_EXP":
                    case "PRINT_IMG":
                    case "PRINT_ITEM":
                    case "PRINT_MARK":
                    case "PRINT_PALAM":
                    case "PRINT_RECT":
                    case "PRINT_SHOPITEM":
                    case "PRINT_SPACE":
                    case "PRINT_TALENT":
                    case "PRINTBUTTON":
                    case "PRINTBUTTONC":
                    case "PRINTBUTTONLC":
                    case "PRINTC":
                    case "PRINTCD":
                    case "PRINTCK":
                    case "PRINTCLENGTH":
                    case "PRINTCPERLINE":
                    case "PRINTD":
                    case "PRINTDL":
                    case "PRINTDW":
                    case "PRINTFORM":
                    case "PRINTFORMC":
                    case "PRINTFORMCD":
                    case "PRINTFORMCK":
                    case "PRINTFORMD":
                    case "PRINTFORMDL":
                    case "PRINTFORMDW":
                    case "PRINTFORMK":
                    case "PRINTFORMKL":
                    case "PRINTFORMKW":
                    case "PRINTFORML":
                    case "PRINTFORMLC":
                    case "PRINTFORMLCD":
                    case "PRINTFORMLCK":
                    case "PRINTFORMS":
                    case "PRINTFORMSD":
                    case "PRINTFORMSDL":
                    case "PRINTFORMSDW":
                    case "PRINTFORMSK":
                    case "PRINTFORMSKL":
                    case "PRINTFORMSKW":
                    case "PRINTFORMSL":
                    case "PRINTFORMSW":
                    case "PRINTFORMW":
                    case "PRINTK":
                    case "PRINTKL":
                    case "PRINTKW":
                    case "PRINTL":
                    case "PRINTLC":
                    case "PRINTLCD":
                    case "PRINTLCK":
                    case "PRINTPLAIN":
                    case "PRINTPLAINFORM":
                    case "PRINTS":
                    case "PRINTSD":
                    case "PRINTSDL":
                    case "PRINTSDW":
                    case "PRINTSINGLE":
                    case "PRINTSINGLED":
                    case "PRINTSINGLEFORM":
                    case "PRINTSINGLEFORMD":
                    case "PRINTSINGLEFORMK":
                    case "PRINTSINGLEFORMS":
                    case "PRINTSINGLEFORMSD":
                    case "PRINTSINGLEFORMSK":
                    case "PRINTSINGLEK":
                    case "PRINTSINGLES":
                    case "PRINTSINGLESD":
                    case "PRINTSINGLESK":
                    case "PRINTSINGLEV":
                    case "PRINTSINGLEVD":
                    case "PRINTSINGLEVK":
                    case "PRINTSK":
                    case "PRINTSKL":
                    case "PRINTSKW":
                    case "PRINTSL":
                    case "PRINTSW":
                    case "PRINTV":
                    case "PRINTVD":
                    case "PRINTVDL":
                    case "PRINTVDW":
                    case "PRINTVK":
                    case "PRINTVKL":
                    case "PRINTVKW":
                    case "PRINTVL":
                    case "PRINTVW":
                    case "PRINTW":
                    case "PUTFORM":
                    case "QUIT":
                    case "RAND":
                    case "RANDOMIZE":
                    case "REDRAW":
                    case "REPLACE":
                    case "RESET_STAIN":
                    case "RESETBGCOLOR":
                    case "RESETCOLOR":
                    case "RESETDATA":
                    case "RESETGLOBAL":
                    case "REUSELASTLINE":
                    case "SAVECHARA":
                    case "SAVEDATA":
                    case "SAVEGAME":
                    case "SAVEGLOBAL":
                    case "SAVENOS":
                    case "SAVETEXT":
                    case "SETANIMETIMER":
                    case "SETBGCOLOR":
                    case "SETBGCOLORBYNAME":
                    case "SETBIT":
                    case "SETCOLOR":
                    case "SETCOLORBYNAME":
                    case "SETFONT":
                    case "SIGN":
                    case "SKIPDISP":
                    case "SORTCHARA":
                    case "SPLIT":
                    case "SPRITEANIMEADDFRAME":
                    case "SPRITEANIMECREATE":
                    case "SPRITECREATE":
                    case "SPRITECREATED":
                    case "SPRITEDISPOSE":
                    case "SPRITEGETCOLOR":
                    case "SPRITEHEIGHT":
                    case "SPRITEMOVE":
                    case "SPRITEPOSX":
                    case "SPRITEPOSY":
                    case "SPRITESETPOS":
                    case "SPRITEWIDTH":
                    case "SQRT":
                    case "STOPCALLTRAIN":
                    case "STRCOUNT":
                    case "STRFIND":
                    case "STRFINDU":
                    case "STRFORM":
                    case "STRLEN":
                    case "STRLENFORM":
                    case "STRLENFORMU":
                    case "STRLENS":
                    case "STRLENSU":
                    case "STRLENU":
                    case "SUBSTRING":
                    case "SUBSTRINGU":
                    case "SUMARRAY":
                    case "SUMCARRAY":
                    case "SWAP":
                    case "SWAPCHARA":
                    case "TIMES":
                    case "TINPUT":
                    case "TINPUTS":
                    case "TOFULL ":
                    case "TOFULL":
                    case "TOHALF ":
                    case "TOHALF":
                    case "TOINT":
                    case "TOLOWER ":
                    case "TOLOWER":
                    case "TONEINPUT":
                    case "TONEINPUTS":
                    case "TOOLTIP_SETCOLOR":
                    case "TOOLTIP_SETDELAY":
                    case "TOOLTIP_SETDURATION":
                    case "TOSTR":
                    case "TOUPPER ":
                    case "TOUPPER":
                    case "TWAIT":
                    case "UNICODE":
                    case "UPCHECK":
                    case "VARSET":
                    case "VARSIZE":
                    case "WAIT":
                    case "WAITANYKEY":
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
                        // emueraに実装された命令・代入可能な変数は上で見ているので、ここまできたら未知の識別子

                        // Function-local UDV
                        if (functionLocalUdv.Contains(ident.ToUpper()))
                            return new Token { Type = LineType.Variable };

                        // GlobalUDV
                        if (erhGlobalUdv.Contains(ident.ToUpper()))
                            return new Token { Type = LineType.ErhUserDefVariable };

                        SkipSpace();

                        // 変数っぽく見えるものは変数と仮定する
                        if (IsVariableSeparator(ss.Current) || ss.Current == '=')
                        {
                            // VAR:XXX ... , VAR = ...
                            //Console.Error.WriteLine($"parse \"{ident}\" as global UDV.");
                            erhGlobalUdv.Add(ident.ToUpper());
                            return new Token { Type = LineType.ErhUserDefVariable };
                        }
                        else if ((ss.Current == '+' || ss.Current == '-' || ss.Current == '*' || ss.Current == '/' || ss.Current == '\'') && ss.Peek(1) == '=')
                        {
                            // VAR += n, VAR -= n, VAR *= n, VAR /= n, VAR '= "..."
                            //Console.Error.WriteLine($"parse \"{ident}\" as global UDV.");
                            erhGlobalUdv.Add(ident.ToUpper());
                            return new Token { Type = LineType.ErhUserDefVariable };
                        }
                        else if ((ss.Current == '+' || ss.Current == '-') && ss.Peek(1) == ss.Current)
                        {
                            // VAR++, VAR--
                            //Console.Error.WriteLine($"parse \"{ident}\" as global UDV.");
                            erhGlobalUdv.Add(ident.ToUpper());
                            return new Token { Type = LineType.ErhUserDefVariable };
                        }
                        else
                        {
                            // 扱いが変数っぽく見えない本当に不明なもの
                            throw new FormatException($"unknown ident name ({(String.IsNullOrWhiteSpace(ident) ? ss.RawString : ident)})");
                        }
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
