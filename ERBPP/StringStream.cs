using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace Whitebell.Library.Utils
{
    /// <summary>文字列を1字ずつ評価するクラス</summary>
    [Serializable]
    public class StringStream : Stream
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly string _str;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private long _ptr;

        #region Constructors

        public StringStream()
        {
            _str = "";
            _ptr = 0;
        }

        public StringStream(string str)
        {
            _str = str ?? "";
            _ptr = 0;
        }

        #endregion

        #region Properties

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        /// <summary>末尾かどうかをしめす値を取得します。</summary>
        public bool EndOfStream => _ptr >= _str.Length;

        /// <summary>内部文字列の文字数を取得します。</summary>
        public override long Length => _str.Length;

        /// <summary>内部ポインタ位置を取得または設定します。</summary>
        /// <exception cref="ArgumentOutOfRangeException">負の位置、または文字列長を越えた位置に内部ポインタを設定しようとした場合</exception>
        public override long Position
        {
            get => _ptr;
            set
            {
                if (value < 0 || _str.Length < value)
                    throw new ArgumentOutOfRangeException(nameof(value));
                _ptr = value;
            }
        }

        /// <summary>内部文字列を取得します。</summary>
        [SuppressMessage("Simplification", "RCS1085:Use auto-implemented property.", Justification = "<Pending>")]
        public string RawString => _str;

        #endregion

        /// <summary>指定した <see cref="Object"/> が現在の <see cref="StringStream"/>  と等しいかどうかを判断します。</summary>
        /// <param name="obj">現在の <see cref="StringStream"/> と比較する <see cref="Object"/>。</param>
        /// <returns>指定した <see cref="Object"/> が現在の <see cref="StringStream"/> と等しい場合は true。それ以外の場合は false。</returns>
        public override bool Equals(object? obj) => obj is StringStream s && Equals(s);

        /// <summary>指定した <see cref="StringStream"/> が現在の <see cref="StringStream"/> と等しいかどうかを判断します。</summary>
        /// <param name="ss">現在の <see cref="StringStream"/> と比較する <see cref="StringStream"/>。</param>
        /// <returns>指定した <see cref="StringStream"/> が現在の <see cref="StringStream"/> と等しい場合は true。それ以外の場合は false。</returns>
        public bool Equals(StringStream ss) => ss is not null && _str == ss._str && _ptr == ss._ptr;

        public override void Flush()
        { /* no-op */ }

        /// <summary>この <see cref="StringStream"/> のハッシュコードを返します。</summary>
        /// <returns>32 ビット符号付整数ハッシュコード</returns>
        public override int GetHashCode() => _str.GetHashCode();

        /// <summary>Reads a sequence of bytes from the current stream and advances the position within the stream by the number of bytes read.</summary>
        /// <param name="buffer">An array of bytes. When this method returns, the buffer contains the specified byte array with the values between offset and (offset + count - 1) replaced by the bytes read from the current source.</param>
        /// <param name="offset">The zero-based byte offset in buffer at which to begin storing the data read from the current stream.</param>
        /// <param name="count">The maximum number of bytes to be read from the current stream.</param>
        /// <returns>The total number of bytes read into the buffer. This can be less than the number of bytes requested if that many bytes are not currently available, or zero (0) if the end of the stream has been reached.</returns>
        /// <exception cref="ArgumentException">The sum of offset and count is larger than the buffer length.</exception>
        /// <exception cref="ArgumentNullException">buffer is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">offset or count is negative.</exception>
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_str.Length < offset + count)
                throw new ArgumentException($"The sum of {nameof(offset)} and {nameof(count)} is larger than the buffer length.");

            if (buffer is null)
                throw new ArgumentNullException(nameof(buffer), $"{nameof(buffer)} is null.");

            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset), $"{nameof(offset)} is negative.");
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count), $"{nameof(count)} is negative.");

            var len = Math.Min((int)_ptr + count, _str.Length);
            var s = _str.Substring((int)_ptr, len);

            for (var i = 0; i < s.Length; i++)
                buffer[offset + i] = (byte)s[i];

            return len;
        }

        /// <summary>末尾でなければ現在の文字をout引数にセットし、ポインタを1進め、<see langword="true"/>を返す。末尾ならばnull文字をセットし<see langword="false"/>を返す。</summary>
        /// <param name="chr"></param>
        public virtual bool NextChar(out char chr)
        {
            _ptr++;
            if (_ptr <= _str.Length)
            {
                chr = _str[(int)_ptr - 1];
                return true;
            }
            else
            {
                chr = '\0';
                return false;
            }
        }

        /// <summary>カウンタを進めずに現在の文字を返す。末尾の場合はnull文字を返す。</summary>
        public char Current => _ptr < _str.Length ? _str[(int)_ptr] : '\0';

        /// <summary>カウンタを進めずに現在の文字を返す。末尾の場合はnull文字を返す。</summary>
        public char Peek() => Current;

        /// <summary>カウンタを進めずに指定した数読み呼ばした先の文字を返す。末尾の場合はnull文字を返す。</summary>
        /// <param name="skipLength"></param>
        public char Peek(int skipLength) => _ptr + skipLength < _str.Length ? _str[(int)_ptr + skipLength] : '\0';

        /// <summary>ポインタ位置を 0 に戻します。</summary>
        public void Reset() => _ptr = 0;

        /// <summary>Sets the current position of this stream to the given value.</summary>
        /// <returns>The new position in the stream.</returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public override long Seek(long offset, SeekOrigin origin)
        {
            var tmp_ptr = _ptr;
            switch (origin)
            {
                case SeekOrigin.Begin:
                    tmp_ptr = offset;
                    break;
                case SeekOrigin.Current:
                    tmp_ptr += offset;
                    break;
                case SeekOrigin.End:
                    tmp_ptr = _str.Length + offset;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(origin));
            }
            if (tmp_ptr < 0 || tmp_ptr > _str.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));
            _ptr = tmp_ptr;
            return _ptr;
        }

        /// <summary>This method is not supported.</summary>
        /// <exception cref="NotSupportedException"></exception>
        public override void SetLength(long value) => throw new NotSupportedException();

        /// <summary>文字列表現。内部文字列を返す。</summary>
        public override string ToString() => _str;

        /// <summary>This method is not supported.</summary>
        /// <exception cref="NotSupportedException"></exception>
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public static implicit operator StringStream(string? s) => new(s ?? "");

        public static explicit operator string(StringStream s) => s is not null ? s.RawString : throw new ArgumentNullException(nameof(s));
    }
}
