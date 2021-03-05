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

    public class ErbLineReader : IDisposable
    {
        private readonly StreamReader reader;

        /// <summary>Gets a value that indicates whether the current read position is at the end of the reader.</summary>
        /// <returns><see langword="true"/> if the current read position is at the end of the reader; otherwise <see langword="false"/>.</returns>
        /// <exception cref="ObjectDisposedException">The underlying stream has been disposed.</exception>
        public bool EndOfReader => reader.EndOfStream;

        public ErbLineReader(Stream stream, Encoding encoding) => reader = new StreamReader(stream, encoding);

        public IErbLine? ReadLine()
        {
            if (reader.EndOfStream)
                return null;

            var line = reader.ReadLine()!.TrimStart();
            var t = new PseudoLexer(line).GetToken().Type;

            switch (t)
            {
                case LineType.StartConcat:
                    break;
                case LineType.Blank:
                    return new ErbBlankLine();
                default:
                    return new ErbLine(t, line);
            }

            var lst = new List<string> { line };
            while (!reader.EndOfStream)
            {
                line = reader.ReadLine()!.TrimStart();
                lst.Add(line);
                t = new PseudoLexer(line).GetToken().Type;
                if (t == LineType.EndConcat)
                    return new ErbConcatLines(t, lst);
                if (t != LineType.Blank)
                    break;
            }
            while (!reader.EndOfStream)
            {
                line = reader.ReadLine()!.TrimStart();
                if (line.StartsWith('}'))
                {
                    lst.Add(line);
                    return new ErbConcatLines(t, lst);
                }
                lst.Add(Constant.ConcatBlockIndent + line);
            }
            throw new FormatException("line concat ({ ... }) hasn't been closed."); // EOSまでたどり着いたケース
        }

        #region IDisposable
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // dispose managed resources
                reader?.Dispose();
            }

            // dispose unmanaged resources
        }

        ~ErbLineReader() => Dispose(false);
        #endregion
    }

    public class ErbLineWriter : IDisposable
    {
        private readonly StreamWriter writer;

        public int IndentLevel { get; set; }

        public bool AutoFlush
        {
            get => writer.AutoFlush;
            set => writer.AutoFlush = value;
        }

        public ErbLineWriter(Stream stream, Encoding encoding) => writer = new StreamWriter(stream, encoding);

        // erbLine.ToIndentStringは末尾改行付きで返すので、ここでWriteLineを使うと不要な改行が入る
        public void Write(IErbLine erbLine) => Write(erbLine, IndentLevel);

        public void Write(IErbLine erbLine, int indentLv) => writer.Write(erbLine.ToIndentString(indentLv));

        /// <summary>Clears all buffers for the current writer and causes any buffered data to be written to the underlying stream.</summary>
        /// <exception cref="ObjectDisposedException">The current writer is closed.</exception>
        /// <exception cref="IOException">An I/O error has occurred.</exception>
        /// <exception cref="EncoderFallbackException">The current encoding does not support displaying half of a Unicode surrogate pair.</exception>
        public void Flush() => writer.Flush();

        #region IDisposable
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // dispose managed resources
                writer?.Dispose();
            }

            // dispose unmanaged resources
        }

        ~ErbLineWriter() => Dispose(false);
        #endregion
    }

    public interface IErbLine
    {
        LineType Type { get; }
        string RawString { get; }

        /// <summary><paramref name="indentLv"/>で指定した深さのインデント付き文字列を返す</summary>
        /// <returns>インデントした改行付き文字列</returns>
        string ToIndentString(int indentLv);
    }

    public class ErbLine : IErbLine
    {
        public LineType Type { get; }
        public string RawString { get; }

        public ErbLine(LineType type, string str)
        {
            Type = type;
            RawString = str;
        }

        public string ToIndentString(int indentLv) => new StringBuilder().Insert(0, Constant.Indent, indentLv).AppendLine(RawString).ToString();
    }

    public class ErbBlankLine : IErbLine
    {
        public LineType Type => LineType.Blank;
        public string RawString => "";

        public string ToIndentString(int indentLv) => Environment.NewLine;
    }

    public class ErbConcatLines : IErbLine
    {
        private readonly List<string> lines;

        public LineType Type { get; }
        public string RawString { get; }

        public ErbConcatLines(LineType type, IList<string> lines)
        {
            Type = type;
            this.lines = new List<string>(lines);
            var sb = new StringBuilder();
            foreach (var l in lines)
                sb.Append(l);
            RawString = sb.ToString();
        }

        public string ToIndentString(int indentLv)
        {
            var sb = new StringBuilder();
            foreach (var l in lines)
            {
                if (String.IsNullOrWhiteSpace(l))
                    sb.AppendLine();
                else
                    sb.Insert(sb.Length, Constant.Indent, indentLv).AppendLine(l);
            }
            return sb.ToString();
        }
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
                            LineType nextType = LineType.Unknown;
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
                                        //if (e.Item1 == LineType.Blank)
                                        if (e.Type == LineType.Blank)
                                        {
                                            //sw.WriteLine();
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
