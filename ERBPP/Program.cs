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
            while ((l = sr.ReadLine()?.TrimStart()) is not null)
            {
            REDO:

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

                    case LineType.StartRegionComment:
                    case LineType.EndRegionComment:
                        sw.WriteLine(l);
                        break;

                    case LineType.If:
                    case LineType.Repeat:
                    case LineType.While:
                    case LineType.For:
                    case LineType.SelectCase:
                    case LineType.PrintData:
                        sw.Write(new string('\t', curIndentLv++));
                        sw.WriteLine(l);
                        break;
                    case LineType.ElseIf:
                    case LineType.Else:
                        sw.Write(new string('\t', curIndentLv - 1));
                        sw.WriteLine(l);
                        break;
                    case LineType.Endif:
                    case LineType.Rend:
                    case LineType.Wend:
                    case LineType.Next:
                    case LineType.EndData:
                        sw.Write(new string('\t', --curIndentLv));
                        sw.WriteLine(l);
                        break;
                    case LineType.Case:
                        if (prevType == LineType.SelectCase)
                            sw.Write(new string('\t', curIndentLv++));
                        else
                            sw.Write(new string('\t', curIndentLv - 1));
                        sw.WriteLine(l);
                        break;
                    case LineType.CaseElse:
                        sw.Write(new string('\t', curIndentLv - 1));
                        sw.WriteLine(l);
                        break;
                    case LineType.EndSelect:
                        curIndentLv -= 2;
                        sw.Write(new string('\t', curIndentLv));
                        sw.WriteLine(l);
                        break;

                    case LineType.Sif:
                        sw.Write(new string('\t', curIndentLv));
                        sw.WriteLine(l);
                        if ((l = sr.ReadLine()?.TrimStart()) is null)
                            throw new FormatException("last line sif");
                        sw.Write(new string('\t', curIndentLv + 1));
                        sw.WriteLine(l);
                        break;

                    case LineType.Comment:
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
                            case LineType.Call:
                            case LineType.Return:
                            case LineType.BuiltinFunction:
                            case LineType.FunctionDefinition:
                            case LineType.Variable:
                            case LineType.Endif:
                            case LineType.If:
                            case LineType.Sif:
                            case LineType.SelectCase:
                                foreach (var e in lst)
                                {
                                    sw.Write(new string('\t', curIndentLv));
                                    sw.WriteLine(e.Item2);
                                }
                                break;
                            case LineType.ElseIf:
                            case LineType.Else:
                            case LineType.Case:
                            case LineType.CaseElse:
                            case LineType.Repeat:
                                foreach (var e in lst)
                                {
                                    if (e.Item1 != LineType.Blank)
                                    {
                                        var indentLv = nextType == LineType.Case && prevType == LineType.SelectCase ? curIndentLv : Math.Max(0, curIndentLv - 1);
                                        sw.Write(new string('\t', indentLv));
                                        sw.WriteLine(e.Item2);
                                    }
                                    else
                                    {
                                        sw.WriteLine();
                                    }
                                }
                                    break;
                            default:
                                throw new NotImplementedException(); // ここにくるような実装を書いていないので仕様を定めていない
                        }
                        l = nl;
                        goto REDO;

                    default:
                        sw.Write(new string('\t', curIndentLv));
                        sw.WriteLine(l);
                        break;
                }

                if (t.Type != LineType.Blank && t.Type != LineType.Comment && t.Type != LineType.StartRegionComment && t.Type != LineType.EndRegionComment)
                    prevType = t.Type;
            }
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
            while (!ss.EndOfStream && Char.IsLetterOrDigit(ss.Current) && !IsVariableSeparator(ss.Current))
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

                var ident = GetIdent().ToLower();

                switch (ident)
                {
                    case "region":
                        return new Token { Type = LineType.StartRegionComment };
                    case "endregion":
                        return new Token { Type = LineType.EndRegionComment };
                    default:
                        return new Token { Type = LineType.Comment };
                }
            }
            else if (IsFunctionStart(ss.Current))
            {
                variable.Clear();
                return new Token { Type = LineType.FunctionDefinition };
            }
            else if (IsAttributeStart(ss.Current))
            {
                Consume('#');
                var ident = GetIdent().ToUpper();
                switch (ident)
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
                            if (!variable.Contains(v))
                                variable.Add(v);
                            return new Token { Type = LineType.VariableDefinition };
                        }
                    default:
                        return new Token { Type = LineType.Unknown };
                }
            }
            else
            {
                var ident = GetIdent().ToUpper();
                switch (ident)
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

                    case "BREAK":
                        return new Token { Type = LineType.Break };

                    case "CALL":
                    case "CALLFORM":
                    case "TRYCALL":
                    case "TRYCALLFORM":
                        return new Token { Type = LineType.Call };

                    case "RETURN":
                    case "RETURNF":
                        return new Token { Type = LineType.Return };

                    case "THROW":
                        return new Token { Type = LineType.Throw };

                    case "PRINTDATA":
                    case "PRINTDATAW":
                        return new Token { Type = LineType.PrintData };
                    case "DATA":
                    case "DATAFORM":
                        return new Token { Type = LineType.Data };
                    case "ENDDATA":
                        return new Token { Type = LineType.EndData };

                    case "DRAWLINE":
                    case "EXISTCSV":
                    case "FONTBOLD":
                    case "FONTREGULAR":
                    case "PRINT":
                    case "PRINTL":
                    case "PRINTW":
                    case "PRINTFORM":
                    case "PRINTFORMDW":
                    case "PRINTFORML":
                    case "PRINTFORMW":
                    case "QUIT":
                    case "SETBIT":
                        return new Token { Type = LineType.BuiltinFunction };

                    case "ARG":
                    case "BASE":
                    case "CFLAG":
                    case "EXP":
                    case "FLAG":
                    case "LOCAL":
                    case "LOCALS":
                    case "RESULT":
                    case "RESULTS":
                    case "SELECTCOM":
                    case "STAIN":
                    case "TALENT":
                    case "TCVAR":
                    case "TFLAG":
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
                        return new Token { Type = LineType.Variable };
                    default:
                        if (variable.Contains(ident))
                            return new Token { Type = LineType.Variable };
                        throw new FormatException("unknown ident name");
                }
            }
        }

        private static bool IsCommentStart(char c) => c == ';';
        private static bool IsFunctionStart(char c) => c == '@';
        private static bool IsAttributeStart(char c) => c == '#';
        private static bool IsVariableSeparator(char c) => c == ':';
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

        Break,

        Call,

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

        Unknown,
    }
}
