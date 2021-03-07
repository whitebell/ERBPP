using System;
using System.IO;
using System.Text;

namespace ERBPP
{
    public class ErbLineWriter : IDisposable
    {
        private readonly StreamWriter writer;

        private int indentLevel;
        public int IndentLevel
        {
            get => indentLevel;
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(value), $"{nameof(value)} is less than zero.");
                indentLevel = value;
            }
        }

        /// <summary>Gets or sets a value indicating whether the <see cref="ErbLineWriter"/> will flush its buffer to the underlying stream after every call to <see cref="ErbLineWriter"/>.Write(<see cref="Char"/>).</summary>
        /// <returns><see langword="true"/> to force <see cref="ErbLineWriter"/> to flush its buffer; otherwise, <see langword="false"/>.</returns>
        public bool AutoFlush
        {
            get => writer.AutoFlush;
            set => writer.AutoFlush = value;
        }

        /// <summary>Initializes a new instance of the <see cref="ErbLineWriter"/> class for the specified stream by using the specified encoding and the default buffer size.</summary>
        /// <param name="stream">The stream to write to.</param>
        /// <param name="encoding">The character encoding to use.</param>
        /// <exception cref="ArgumentNullException"><paramref name="stream"/> or <paramref name="encoding"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="stream"/> is not writable.</exception>
        public ErbLineWriter(Stream stream, Encoding encoding) => writer = new StreamWriter(stream, encoding);

        /// <summary>Writes a erbline to the stream.</summary>
        /// <param name="erbLine">The erbline object to write to the stream.</param>
        /// <exception cref="ObjectDisposedException"><see cref="AutoFlush"/> is <see langword="true"/> or the <see cref="ErbLineWriter"/> buffer is full, and current writer is closed.</exception>
        /// <exception cref="NotSupportedException"><see cref="AutoFlush"/> is <see langword="true"/> or the <see cref="ErbLineWriter"/> buffer is full, and the contents of the buffer cannot be written to the underlying fixed size stream because the <see cref="ErbLineWriter"/> is at the end the stream.</exception>
        /// <exception cref="IOException">An I/O error occurs.</exception>
        public void Write(IErbLine erbLine) => writer.Write(erbLine.ToIndentString(IndentLevel)); // erbLine.ToIndentStringは末尾改行付きで返すので、ここでStreamWriter.WriteLineを使うと不要な改行が入る

        /// <summary>Writes a erbline to the stream, with specified indent.</summary>
        /// <param name="erbLine">The erbline object to write to the stream.</param>
        /// <param name="indentLv">The depth of indent.</param>
        /// <exception cref="ObjectDisposedException"><see cref="AutoFlush"/> is <see langword="true"/> or the <see cref="ErbLineWriter"/> buffer is full, and current writer is closed.</exception>
        /// <exception cref="NotSupportedException"><see cref="AutoFlush"/> is <see langword="true"/> or the <see cref="ErbLineWriter"/> buffer is full, and the contents of the buffer cannot be written to the underlying fixed size stream because the <see cref="ErbLineWriter"/> is at the end the stream.</exception>
        /// <exception cref="IOException">An I/O error occurs.</exception>
        /// <exception cref="ArgumentException"><paramref name="indentLv"/> is less than 0.</exception>
        public void Write(IErbLine erbLine, int indentLv) => writer.Write(erbLine.ToIndentString(indentLv)); // erbLine.ToIndentStringは末尾改行付きで返すので、ここでStreamWriter.WriteLineを使うと不要な改行が入る

        /// <summary>Clears all buffers for the current writer and causes any buffered data to be written to the underlying stream.</summary>
        /// <exception cref="ObjectDisposedException">The current writer is closed.</exception>
        /// <exception cref="IOException">An I/O error has occurred.</exception>
        /// <exception cref="EncoderFallbackException">The current encoding does not support displaying half of a Unicode surrogate pair.</exception>
        public void Flush() => writer.Flush();

        #region IDisposable
        /// <summary>Releases all resources used by the <see cref="ErbLineWriter"/> object.</summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>Releases the unmanaged resources used by the <see cref="ErbLineWriter"/> and optionally releases the managed resources.</summary>
        /// <param name="disposing"><see langword="true"/> to release both managed and unmanaged resources; <see langword="false"/> to release only unmanaged resources.</param>
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
