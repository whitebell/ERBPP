using System;
using System.Collections.Generic;
using System.Text;
using Whitebell.Library.Utils;

namespace ERBPP
{
    public record Token(LineType Type);

    public class PseudoLexer
    {
        private readonly StringStream ss;
        private readonly string position;

        private static readonly HashSet<string> functionLocalUdv = new();
        private static readonly HashSet<string> erhGlobalUdv = new();

        public PseudoLexer(string s, string position)
        {
            ss = new StringStream(s.TrimEnd('\r', '\n'));
            this.position = position;
        }

        private void SkipSpace()
        {
            while (!ss.EndOfStream && Char.IsWhiteSpace(ss.Current))
                ss.NextChar(out _);
        }

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
                return new Token(LineType.Blank);
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
                    "region" => new Token(LineType.StartRegionComment),
                    "endregion" => new Token(LineType.EndRegionComment),
                    _ => new Token(LineType.Comment),
                };
            }
            else if (IsFunctionStart(ss.Current))
            {
                functionLocalUdv.Clear();
                return new Token(LineType.FunctionDefinition);
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
                        return new Token(LineType.Attribute);
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
                            return functionLocalUdv.Add(v) ? new Token(LineType.VariableDefinition) : throw new FormatException($"{v} is already defined. ({position})");
                        }
                    default:
                        throw new FormatException($"unknown attribute. ({ident}) ({position})");
                }
            }
            else if (IsLabelStart(ss.Current))
            {
                return new Token(LineType.Label);
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
                    case "ELSE":
                    case "ENDIF":
                    case "IF_DEBUG":
                    case "IF_NDEBUG":
                        return IsSpBlockEnd(ss.Current) ? new Token(LineType.SpBlock) : throw new FormatException($"unknown spblock: {ident} ({position})");
                    case "IF":
                    case "ELSEIF":
                        SkipSpace();
                        GetIdent();
                        SkipSpace();
                        return IsSpBlockEnd(ss.Current) ? new Token(LineType.SpBlock) : throw new FormatException($"unknown spblock: {ident} ({position})");
                    default:
                        throw new FormatException($"unknown spblock: {ident} ({position})");
                }
            }
            else if (IsConcatStart(ss.Current))
            {
                return new Token(LineType.StartConcat);
            }
            else if (IsConcatEnd(ss.Current))
            {
                return new Token(LineType.EndConcat);
            }
            else if (IsIncr(ss.Current))
            {
                if (!IsIncr(ss.Peek(1)))
                    throw new FormatException($"can't parse. {ss.RawString} ({position})");

                Consume('+');
                Consume('+');
                SkipSpace();
                var t = GetToken();
                return t.Type switch
                {
                    LineType.Variable or LineType.ErhUserDefVariable => new Token(t.Type),
                    _ => throw new FormatException($"incr op. + nonvariable ({position})"),
                };
            }
            else if (IsDecr(ss.Current))
            {
                if (!IsDecr(ss.Peek(1)))
                    throw new FormatException($"can't parse. {ss.RawString} ({position})");

                Consume('-');
                Consume('-');
                SkipSpace();
                var t = GetToken();
                return t.Type switch
                {
                    LineType.Variable or LineType.ErhUserDefVariable => new Token(t.Type),
                    _ => throw new FormatException($"decr op. + nonvariable ({position})"),
                };
            }
            else
            {
                var ident = GetIdent(); // 小文字のユーザ定義変数があった。ここで大文字にしてしまうとdefaultのUDVを見るところで誤って見落としてしまう。辞書に入れる時点で全部大文字にしてしまうか。
                switch (ident.ToUpper())
                {
                    case "SIF":
                        return new Token(LineType.Sif);

                    case "IF":
                        return new Token(LineType.If);
                    case "ELSEIF":
                        return new Token(LineType.ElseIf);
                    case "ELSE":
                        return new Token(LineType.Else);
                    case "ENDIF":
                        return new Token(LineType.Endif);

                    case "REPEAT":
                        return new Token(LineType.Repeat);
                    case "REND":
                        return new Token(LineType.Rend);

                    case "SELECTCASE":
                        return new Token(LineType.SelectCase);
                    case "CASE":
                        return new Token(LineType.Case);
                    case "CASEELSE":
                        return new Token(LineType.CaseElse);
                    case "ENDSELECT":
                        return new Token(LineType.EndSelect);

                    case "FOR":
                        return new Token(LineType.For);
                    case "NEXT":
                        return new Token(LineType.Next);

                    case "WHILE":
                        return new Token(LineType.While);
                    case "WEND":
                        return new Token(LineType.Wend);

                    case "DO":
                        return new Token(LineType.Do);
                    case "LOOP":
                        return new Token(LineType.Loop);

                    case "BREAK":
                        return new Token(LineType.Break);

                    case "CONTINUE":
                        return new Token(LineType.Continue);

                    case "TRYCALLLIST":
                        return new Token(LineType.TryCallList);
                    case "TRYGOTOLIST":
                        return new Token(LineType.TryGotoList);
                    case "TRYJUMPLIST":
                        return new Token(LineType.TryJumpList);
                    case "FUNC":
                        return new Token(LineType.Func);
                    case "ENDFUNC":
                        return new Token(LineType.EndFunc);

                    case "TRYCCALL":
                    case "TRYCCALLFORM":
                        return new Token(LineType.TryCCall);
                    case "TRYCGOTO":
                    case "TRYCGOTOFORM":
                        return new Token(LineType.TryCGoto);
                    case "TRYCJUMP":
                    case "TRYCJUMPFORM":
                        return new Token(LineType.TryCJump);
                    case "CATCH":
                        return new Token(LineType.Catch);
                    case "ENDCATCH":
                        return new Token(LineType.EndCatch);

                    case "BEGIN":
                        return new Token(LineType.Begin);

                    case "RESTART":
                        return new Token(LineType.Restart);

                    case "CALL":
                    case "CALLF":
                    case "CALLFORM":
                    case "CALLFORMF":
                    case "TRYCALL":
                    case "TRYCALLFORM":
                        return new Token(LineType.Call);

                    case "JUMP":
                    case "JUMPFORM":
                    case "TRYJUMP":
                    case "TRYJUMPFORM":
                        return new Token(LineType.Jump);

                    case "GOTO":
                    case "GOTOFORM":
                    case "TRYGOTO":
                    case "TRYGOTOFORM":
                        return new Token(LineType.Goto);

                    case "RETURN":
                    case "RETURNF":
                    case "RETURNFORM":
                        return new Token(LineType.Return);

                    case "THROW":
                        return new Token(LineType.Throw);

                    case "PRINTDATA":
                    case "PRINTDATAD":
                    case "PRINTDATADL":
                    case "PRINTDATADW":
                    case "PRINTDATAK":
                    case "PRINTDATAKL":
                    case "PRINTDATAKW":
                    case "PRINTDATAL":
                    case "PRINTDATAW":
                        return new Token(LineType.PrintData);
                    case "STRDATA":
                        return new Token(LineType.StrData);
                    case "DATALIST":
                        return new Token(LineType.DataList);
                    case "ENDLIST":
                        return new Token(LineType.EndList);
                    case "DATA":
                    case "DATAFORM":
                        return new Token(LineType.Data);
                    case "ENDDATA":
                        return new Token(LineType.EndData);

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
                        return new Token(LineType.BuiltinFunction);

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
                        return new Token(LineType.Variable);

                    default:
                        // emueraに実装された命令・代入可能な変数は上で見ているので、ここまできたら未知の識別子

                        // Function-local UDV
                        if (functionLocalUdv.Contains(ident.ToUpper()))
                            return new Token(LineType.Variable);

                        // GlobalUDV
                        if (erhGlobalUdv.Contains(ident.ToUpper()))
                            return new Token(LineType.ErhUserDefVariable);

                        SkipSpace();

                        // 変数っぽく見えるものは変数と仮定する
                        if (IsVariableSeparator(ss.Current) || ss.Current == '=')
                        {
                            // VAR:XXX ... , VAR = ...
                            erhGlobalUdv.Add(ident.ToUpper());
                            return new Token(LineType.ErhUserDefVariable);
                        }
                        else if ((ss.Current == '+' || ss.Current == '-' || ss.Current == '*' || ss.Current == '/' || ss.Current == '\'') && ss.Peek(1) == '=')
                        {
                            // VAR += n, VAR -= n, VAR *= n, VAR /= n, VAR '= "..."
                            erhGlobalUdv.Add(ident.ToUpper());
                            return new Token(LineType.ErhUserDefVariable);
                        }
                        else if ((ss.Current == '+' || ss.Current == '-') && ss.Peek(1) == ss.Current)
                        {
                            // VAR++, VAR--
                            erhGlobalUdv.Add(ident.ToUpper());
                            return new Token(LineType.ErhUserDefVariable);
                        }
                        else
                        {
                            // 扱いが変数っぽく見えない本当に不明なもの
                            throw new FormatException($"unknown ident name ({(String.IsNullOrWhiteSpace(ident) ? ss.RawString : ident)}) ({position})");
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
        private static bool IsConcatStart(char c) => c == '{';
        private static bool IsConcatEnd(char c) => c == '}';
    }
}
