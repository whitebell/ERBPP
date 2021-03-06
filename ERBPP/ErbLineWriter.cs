using System;
using System.IO;
using System.Text;

namespace ERBPP
{
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

        // erbLine.ToIndentStringは末尾改行付きで返すので、ここでStreamWriter.WriteLineを使うと不要な改行が入る
        public void Write(IErbLine erbLine) => writer.Write(erbLine.ToIndentString(IndentLevel));
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
}
