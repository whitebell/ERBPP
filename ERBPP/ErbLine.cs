using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace ERBPP
{
    public interface IErbLine
    {
        LineType Type { get; }
        string RawString { get; }

        /// <summary><paramref name="indentLv"/>で指定した深さのインデント付き文字列を返す</summary>
        /// <param name="indentLv">インデントの深さ</param>
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

    public sealed class ErbBlankLine : IErbLine
    {
        public LineType Type => LineType.Blank;
        public string RawString => "";

        private ErbBlankLine() { }

        public static ErbBlankLine Instance { get; } = new();

        public string ToIndentString(int indentLv) => Environment.NewLine;
    }

    public class ErbConcatLines : IErbLine
    {
        private readonly List<string> lines;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string? rawString;

        public LineType Type { get; }

        public string RawString => rawString ??= GetRawString();

        private string GetRawString()
        {
            var sb = new StringBuilder();
            foreach (var l in lines)
                sb.Append(l); // todo: InvalidOpExcp. の時にしか呼ばれないけど、単純に連結でいいのか考える
            return sb.ToString();
        }

        public ErbConcatLines(LineType type, IReadOnlyList<string> lines)
        {
            Type = type;
            this.lines = new List<string>(lines);
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
}
