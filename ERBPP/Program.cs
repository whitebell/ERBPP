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
            // .NET環境ではUS-ASCII, ISO-8859-1, UTF-8, UTF-16LE/BE, UTF-32LE/BEのみ
            //foreach (var ei in Encoding.GetEncodings())
            //    Console.WriteLine($"{ei.DisplayName}\t{ei.CodePage}\t{ei.Name}");
            // Shift_JISなど他のエンコーディングが必要な時はnugetからSystem.Text.Encoding.CodePagesをインストールして下の行を有効にする
            // あとは適当に実装する
            //Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

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
                        // 関数定義行でインデントされているのはおかしいので例外投げる
                        if (ew.IndentLevel != 0)
                            throw new FormatException("indented func def.");
                        ew.Write(el);
                        break;
                    case LineType.VariableDefinition:
                        // 変数定義行でインデントされているのはおかしいので例外投げる
                        if (ew.IndentLevel != 0)
                            throw new FormatException("indented var def.");
                        // 関数の先頭で定義されなければいけない
                        if (prevType is not LineType.FunctionDefinition and not LineType.VariableDefinition and not LineType.Attribute)
                            throw new FormatException("User-defined variable must be defined at the beginning of the function.");
                        ew.Write(el);
                        break;
                    case LineType.Attribute:
                        // 関数属性定義行でインデントされているのはおかしいので例外投げる
                        if (ew.IndentLevel != 0)
                            throw new FormatException("indented attr def.");
                        // 関数の先頭で定義されなければいけない
                        if (prevType is not LineType.FunctionDefinition and not LineType.VariableDefinition and not LineType.Attribute)
                            throw new FormatException("function attribute must be defined at the beginning of the function.");
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
                        ew.Write(el, ew.IndentLevel - 1);
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
                            ew.Write(el, ew.IndentLevel++); // LineType.CaseElseでここにくるのは、SELECTCASE中にCASEがなく、CASEELSEのみのケース。正気じゃないと思うが、実例があるのでしょうがない。eratohoJ+ REVMODE.ERB
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
                            // Emueraとmakerで仕様に差があり、Emueraはコメント行・空行を無視して有効な命令の行を処理する。
                            // Emueraに合わせたインデントをかける。
                            // https://ja.osdn.net/projects/emuera/wiki/diff#h5-SIF.E3.81.AE.E7.9B.B4.E5.BE.8C.E3.81.8C.E7.A9.BA.E8.A1.8C.E3.83.BB.E3.82.B3.E3.83.A1.E3.83.B3.E3.83.88.E8.A1.8C.E3.81.AA.E3.81.A9.E3.81.A7.E3.81.82.E3.82.8B.E5.A0.B4.E5.90.88
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
                            while (!er.EndOfReader)
                            {
                                el = er.ReadLine()!;
                                if (el.Type is not LineType.Comment and not LineType.StartRegionComment and not LineType.EndRegionComment)
                                    break;
                                lst.Add(el);
                            }
                            var indentLv = el.Type switch
                            {
                                LineType.ElseIf or LineType.Else or LineType.EndSelect => ew.IndentLevel - 1,
                                LineType.Case or LineType.CaseElse => prevType == LineType.SelectCase ? ew.IndentLevel : ew.IndentLevel - 1,
                                _ => ew.IndentLevel,
                            };
                            foreach (var e in lst)
                            {
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
                                    case LineType.Comment:
                                        ew.Write(e, indentLv);
                                        break;
                                    default:
                                        throw new InvalidOperationException();
                                }
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
