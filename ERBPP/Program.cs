﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ERBPP
{
    public static class Program
    {
        public static void Main()
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
            while (!sr.EndOfStream)
            {
                l = sr.ReadLine()!.TrimStart(); // !sr.EndOfStream. sr.ReadLine() returns string.
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

                    case LineType.StartConcat:
                        {
                            sw.Write(new string('\t', curIndentLv));
                            sw.WriteLine(l);

                            if (sr.EndOfStream)
                                throw new FormatException("last line concat. '{' ().");

                            l = sr.ReadLine()!.TrimStart(); // !sr.EndOfStream. sr.ReadLine() returns string.
                            t = new PseudoLexer(l).GetToken();
                            if (t.Type != LineType.EndConcat)
                            {
                                sw.Write(new string('\t', curIndentLv++));
                                sw.WriteLine(l);
                            }
                            while (!sr.EndOfStream)
                            {
                                // 2行目以降は連結しないと意味が取れない不完全行なので、PseudoLexerには食べさせない
                                l = sr.ReadLine()!.TrimStart(); // !sr.EndOfStream. sr.ReadLine() returns string.
                                if (l.StartsWith('}'))
                                    goto READLINE_REDO;
                                sw.Write(new string('\t', curIndentLv));
                                sw.WriteLine(l);
                            }
                            throw new FormatException("line concat ({ ... }) hasn't been closed.");
                        }
                    case LineType.EndConcat:
                        sw.Write(new string('\t', --curIndentLv));
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
                            var lst = new List<(LineType, string)>();
                            while (!sr.EndOfStream)
                            {
                                l = sr.ReadLine()!.TrimStart(); // !sr.EndOfStream. sr.ReadLine() returns string.
                                t = new PseudoLexer(l).GetToken();
                                lst.Add((t.Type, l));
                                if (t.Type != LineType.Comment && t.Type != LineType.Blank && t.Type != LineType.StartRegionComment && t.Type != LineType.EndRegionComment)
                                    break;
                            }
                            foreach (var e in lst)
                            {
                                switch (e.Item1)
                                {
                                    case LineType.Blank:
                                        sw.WriteLine();
                                        break;
                                    default:
                                        // Start/EndRegionCommentあたり怪しいかもしれない
                                        sw.Write(new string('\t', curIndentLv + 1));
                                        sw.WriteLine(e.Item2);
                                        break;
                                }
                            }
                        }
                        break;

                    case LineType.Comment:
                    case LineType.StartRegionComment:
                        {
                            var lst = new List<(LineType, string)> { (t.Type, l) };
                            string nl;
                            var nextType = LineType.Unknown;
                            while (!sr.EndOfStream)
                            {
                                nl = sr.ReadLine()!.TrimStart(); // !sr.EndOfStream. sr.ReadLine() returns string.
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
                                        l = nl;
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
                        }
                        if (sr.EndOfStream)
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
}
