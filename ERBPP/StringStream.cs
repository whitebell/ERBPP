using System;
using System.Diagnostics;
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
        public string RawString => _str;

        #endregion

        /// <summary>指定した <see cref="Object"/> が現在の <see cref="StringStream"/>  と等しいかどうかを判断します。</summary>
        /// <param name="obj">現在の <see cref="StringStream"/> と比較する <see cref="Object"/>。</param>
        /// <returns>指定した <see cref="Object"/> が現在の <see cref="StringStream"/> と等しい場合は true。それ以外の場合は false。</returns>
        public override bool Equals(object obj) => obj is StringStream s && Equals(s);

        /// <summary>指定した <see cref="StringStream"/> が現在の <see cref="StringStream"/> と等しいかどうかを判断します。</summary>
        /// <param name="ss">現在の <see cref="StringStream"/> と比較する <see cref="StringStream"/>。</param>
        /// <returns>指定した <see cref="StringStream"/> が現在の <see cref="StringStream"/> と等しい場合は true。それ以外の場合は false。</returns>
        public bool Equals(StringStream ss) => ss is not null && _str == ss._str && _ptr == ss._ptr;

        public override void Flush()
        { /* no-op */ }

        /// <summary>この <see cref="StringStream"/> のハッシュコードを返します。</summary>
        /// <returns>32 ビット符号付整数ハッシュコード</returns>
        public override int GetHashCode() => _str.GetHashCode();

        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_str.Length < offset + count)
                throw new ArgumentOutOfRangeException($"{nameof(offset)} and/or {nameof(count)}" ,"The sum of offset and count is larger than the buffer length.");

            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));

            var len = Math.Min((int)_ptr + count, _str.Length);
            var s = _str.Substring((int)_ptr, len);

            for (var i = 0; i < s.Length; i++)
                buffer[offset + i] = (byte)s[i];

            return len;
        }

        /// <summary>末尾でなければ現在の文字をout引数にセットし、ポインタを1進める。末尾ならばnull文字をセットする。</summary>
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

        /// <param name="offset"></param>
        /// <param name="origin"></param>
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

        /// <param name="value"></param>
        /// <exception cref="NotSupportedException"></exception>
        public override void SetLength(long value) => throw new NotSupportedException();

        /// <summary>文字列表現。内部文字列を返す。</summary>
        public override string ToString() => _str;

        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <exception cref="NotSupportedException"></exception>
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public static implicit operator StringStream(string s) => new StringStream(s ?? "");

        public static explicit operator string(StringStream s) => s is not null ? s.RawString : throw new ArgumentNullException(nameof(s));
    }
}
