using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ERBPP
{
    public static class Constant
    {
        public const string Indent = "\t";
        public const string ConcatBlockIndent = "\t";
    }

    public static class Program
    {
        public static void Main()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

#if DEBUG
            //using var fr = File.Open("D:\\Game\\eratoho\\EVENT_K14\\EVENT_K14.ERB", FileMode.Open, FileAccess.Read);
            using var fr = File.Open(@"D:\Repository\whitebell\ERBPP\TESTCASE.ERB", FileMode.Open, FileAccess.Read);
            using var er = new ErbLineReader(fr, Encoding.UTF8);
            //using var fw = File.Open("D:\\Game\\eratoho\\EVENT_K14\\EVENT_K14.PP.ERB", FileMode.Create, FileAccess.Write);
            using var fw = File.Open(@"D:\Repository\whitebell\ERBPP\TESTCASE.PP.ERB", FileMode.Create, FileAccess.Write);
            using var ew = new ErbLineWriter(fw, Encoding.UTF8) { AutoFlush = true };
#elif RELEASE
            using var er = new ErbLineReader(Console.OpenStandardInput(), Encoding.UTF8);
            using var ew = new ErbLineWriter(Console.OpenStandardOutput(), Encoding.UTF8) { AutoFlush = true };
#endif

            IErbLine el;
            var prevType = LineType.Unknown;
            var regionStack = new Stack<int>();
            while (!er.EndOfReader)
            {
                el = er.ReadLine()!; // !er.EOR
            READLINE_REDO:
                switch (el.Type)
                {
                    case LineType.Blank:
                        ew.Write(el);
                        break;

                    case LineType.FunctionDefinition:
                        if (ew.IndentLevel != 0)
                            throw new FormatException("indented func def."); // 関数定義行でインデントされているのはおかしいので例外投げる
                        ew.Write(el);
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
                        ew.Write(el, ew.IndentLevel++);
                        break;
                    case LineType.ElseIf:
                    case LineType.Else:
                    case LineType.Catch:
                        ew.Write(el, ew.IndentLevel- 1);
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
                        ew.Write(el, --ew.IndentLevel);
                        break;
                    case LineType.Case:
                    case LineType.CaseElse:
                        if (prevType == LineType.SelectCase)
                            ew.Write(el, ew.IndentLevel++); // LineType.CaseElseでここにくるのは正気じゃないと思うが、実例があるのでしょうがない。eratohoJ+ REVMODE.ERB
                        else
                            ew.Write(el, ew.IndentLevel - 1);
                        break;
                    case LineType.DataList:
                        ew.Write(el, ew.IndentLevel++);
                        break;
                    case LineType.EndSelect:
                        ew.IndentLevel -= prevType == LineType.SelectCase ? 1 : 2; // eraTW ERB/MOVEMENTS/物件関連/JOB_MANAGE.ERB CASE,CASEELSEがない空のSELECTCASE～ENDSELECT。正気じゃない。
                        ew.Write(el);
                        break;

                    case LineType.Sif:
                        {
                            ew.Write(el);

                            // eratohoJ+ COMF140.ERB SIFの次にコメント行。しぬべき。
                            var lst = new List<IErbLine>();
                            while (!er.EndOfReader)
                            {
                                el = er.ReadLine()!;
                                lst.Add(el);
                                if (el.Type != LineType.Blank && el.Type != LineType.Comment && el.Type != LineType.StartRegionComment && el.Type != LineType.EndRegionComment)
                                    break;
                            }
                            foreach (var e in lst)
                            {
                                switch (e.Type)
                                {
                                    case LineType.Blank:
                                        ew.Write(e);
                                        break;
                                    default:
                                        ew.Write(e, ew.IndentLevel + 1); // Start/EndRegionCommentあたり怪しいかもしれない
                                        break;
                                }
                            }
                        }
                        break;

                    case LineType.Comment:
                    case LineType.StartRegionComment:
                        {
                            var lst = new List<IErbLine> { el };
                            IErbLine nextLine;
                            var nextType = LineType.Unknown;
                            while (!er.EndOfReader)
                            {
                                nextLine = er.ReadLine()!;
                                switch (nextLine.Type)
                                {
                                    case LineType.Comment:
                                    case LineType.StartRegionComment:
                                    case LineType.EndRegionComment:
                                        lst.Add(nextLine);
                                        break;
                                    default:
                                        el = nextLine;
                                        nextType = nextLine.Type;
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
                                        if (e.Type == LineType.Blank)
                                        {
                                            ew.Write(e);
                                        }
                                        else
                                        {
                                            var indentLv = (nextType == LineType.Case || nextType == LineType.CaseElse) && prevType == LineType.SelectCase ? ew.IndentLevel : Math.Max(0, ew.IndentLevel - 1);
                                            switch (e.Type)
                                            {
                                                case LineType.StartRegionComment:
                                                    regionStack.Push(indentLv);
                                                    ew.Write(e, indentLv);
                                                    break;
                                                case LineType.EndRegionComment:
                                                    {
                                                        if (!regionStack.TryPop(out var res))
                                                            throw new FormatException("region/endregion stack err.");
                                                        ew.Write(e, res);
                                                    }
                                                    break;
                                                default:
                                                    ew.Write(e, indentLv);
                                                    break;
                                            }
                                        }
                                    }
                                    break;
                                default:
                                    foreach (var e in lst)
                                    {
                                        switch (e.Type)
                                        {
                                            case LineType.Blank:
                                                ew.Write(e);
                                                break;
                                            case LineType.StartRegionComment:
                                                regionStack.Push(ew.IndentLevel);
                                                ew.Write(e);
                                                break;
                                            case LineType.EndRegionComment:
                                                {
                                                    if (!regionStack.TryPop(out var res))
                                                        throw new FormatException("region/endregion stack err.");
                                                    ew.Write(e, res);
                                                }
                                                break;
                                            default:
                                                ew.Write(e);
                                                break;
                                        }
                                    }
                                    break;
                            }
                        }
                        if (er.EndOfReader)
                            goto READLINE_BREAK;

                        goto READLINE_REDO;
                    case LineType.EndRegionComment:
                        {
                            if (!regionStack.TryPop(out var res))
                                throw new FormatException("region/endregion stack err.");
                            ew.Write(el, res);
                        }
                        break;

                    case LineType.Unknown:
                        throw new FormatException($"unknown line type. ({el.RawString})");

                    case LineType.StartConcat:
                    case LineType.EndConcat:
                        throw new ArgumentException(); // ErbConcatLineに内包されてしまっているはずなのでここにくるのは完全にバグ。

                    default:
                        ew.Write(el);
                        break;
                }

                if (el.Type != LineType.Blank && el.Type != LineType.Comment && el.Type != LineType.StartRegionComment && el.Type != LineType.EndRegionComment)
                    prevType = el.Type;
                ew.Flush();
            }
        READLINE_BREAK:

            if (regionStack.Count != 0)
                throw new FormatException($"region/endregion stack err. c={regionStack.Count}");
        }
    }
}
