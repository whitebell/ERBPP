using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ERBPP
{
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
}
