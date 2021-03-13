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

        public int LineNum { get; private set; }

#if DEBUG
        public string Position => reader.BaseStream is FileStream fs ? $"line.{LineNum} ({fs.Name})" : $"line.{LineNum}";
#else
        public string Position => $"line.{LineNum}";
#endif

        /// <summary>Initializes a new instance of the <see cref="ErbLineReader"/> class for the specified stream, with the specified character encoding.</summary>
        /// <param name="stream">The stream to be read.</param>
        /// <param name="encoding">The character encoding to use.</param>
        /// <exception cref="ArgumentException">stream does not support reading.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="stream"/> or <paramref name="encoding"/> is <see langword="null"/>.</exception>
        public ErbLineReader(Stream stream, Encoding encoding) => reader = new StreamReader(stream, encoding);

        /// <summary>Reads a logical line from the current stream and returns the data as a IErbLine.</summary>
        /// <returns>The next logical line from the input stream, or <see langword="null"/> if the end of the input stream is reached.</returns>
        /// <exception cref="OutOfMemoryException">There is insufficient memory to allocate a buffer for the returned string.</exception>
        /// <exception cref="IOException">An I/O error occurs.</exception>
        /// <exception cref="FormatException">There is a invalid line.</exception>
        public IErbLine? ReadLine()
        {
            if (reader.EndOfStream)
                return null;

            var line = reader.ReadLine()!.TrimStart();
            LineNum++;
            var t = new PseudoLexer(line, Position).GetToken().Type;

            switch (t)
            {
                case LineType.StartConcat:
                    break;
                case LineType.Blank:
                    return new ErbBlankLine();
                default:
                    return new ErbLine(t, line);
            }

            // Concat Block
            var lst = new List<string> { line };
            while (!reader.EndOfStream)
            {
                line = reader.ReadLine()!.TrimStart();
                LineNum++;
                lst.Add(line);
                t = new PseudoLexer(line, Position).GetToken().Type;
                if (t == LineType.EndConcat)
                    return new ErbConcatLines(LineType.Blank, lst); // Blank {}
                if (t != LineType.Blank)
                    break;
            }
            while (!reader.EndOfStream)
            {
                line = reader.ReadLine()!.TrimStart();
                LineNum++;
                if (line.StartsWith('}'))
                {
                    lst.Add(line);
                    return new ErbConcatLines(t, lst);
                }
                lst.Add(Constant.ConcatBlockIndent + line);
            }
            throw new FormatException($"line concat ({{ ... }}) hasn't been closed. ({Position})"); // EOSまでたどり着いたケース
        }

#region IDisposable
        /// <summary>Releases all resources used by the <see cref="ErbLineReader"/> object.</summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases the unmanaged resources used by the <see cref="ErbLineReader"/> and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing"><see langword="true"/> to release both managed and unmanaged resources; <see langword="false"/> to release only unmanaged resources.</param>
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
