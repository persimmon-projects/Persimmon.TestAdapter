using System;
using System.Threading;

namespace Persimmon.TestRunner.Internals
{
    public sealed class RemoteCancellationToken
        : MarshalByRefObject,
        IConvertible
    {
        private readonly CancellationToken token_;

        private RemoteCancellationToken(CancellationToken token)
        {
            token_ = token;
        }

        public void InternalRegisterSink(InternalRemoteCancellationTokenSink sink)
        {
            token_.Register(() =>
            {
                try
                {
                    sink.Cancel();
                }
                catch
                {
                    // Ignore remote call if raised. (May be discarded AppDomain)
                }
            });
        }

        public static CancellationToken AsToken(RemoteCancellationToken remoteToken)
        {
            var sink = new InternalRemoteCancellationTokenSink();
            remoteToken.InternalRegisterSink(sink);
            return sink.Token;
        }

        public static implicit operator CancellationToken(RemoteCancellationToken remoteToken)
        {
            return AsToken(remoteToken);
        }

        public static implicit operator RemoteCancellationToken(CancellationToken token)
        {
            return new RemoteCancellationToken(token);
        }

        public sealed class InternalRemoteCancellationTokenSink
            : MarshalByRefObject
        {
            private readonly CancellationTokenSource cts_ = new CancellationTokenSource();

            internal InternalRemoteCancellationTokenSink()
            {
            }

            internal CancellationToken Token
            {
                get { return cts_.Token; }
            }

            public void Cancel()
            {
                cts_.Cancel();
            }
        }

        public TypeCode GetTypeCode()
        {
            return TypeCode.Object;
        }

        public bool ToBoolean(IFormatProvider provider)
        {
            return ThrowInvalidCast<bool>();
        }

        public char ToChar(IFormatProvider provider)
        {
            return ThrowInvalidCast<char>();
        }

        public sbyte ToSByte(IFormatProvider provider)
        {
            return ThrowInvalidCast<sbyte>();
        }

        public byte ToByte(IFormatProvider provider)
        {
            return ThrowInvalidCast<byte>();
        }

        public short ToInt16(IFormatProvider provider)
        {
            return ThrowInvalidCast<short>();
        }

        public ushort ToUInt16(IFormatProvider provider)
        {
            return ThrowInvalidCast<ushort>();
        }

        public int ToInt32(IFormatProvider provider)
        {
            return ThrowInvalidCast<int>();
        }

        public uint ToUInt32(IFormatProvider provider)
        {
            return ThrowInvalidCast<uint>();
        }

        public long ToInt64(IFormatProvider provider)
        {
            return ThrowInvalidCast<long>();
        }

        public ulong ToUInt64(IFormatProvider provider)
        {
            return ThrowInvalidCast<ulong>();
        }

        public float ToSingle(IFormatProvider provider)
        {
            return ThrowInvalidCast<float>();
        }

        public double ToDouble(IFormatProvider provider)
        {
            return ThrowInvalidCast<double>();
        }

        public decimal ToDecimal(IFormatProvider provider)
        {
            return ThrowInvalidCast<decimal>();
        }

        public DateTime ToDateTime(IFormatProvider provider)
        {
            return ThrowInvalidCast<DateTime>();
        }

        public string ToString(IFormatProvider provider)
        {
            return ThrowInvalidCast<string>();
        }

        public object ToType(Type conversionType, IFormatProvider provider)
        {
            if (conversionType == typeof(RemoteCancellationToken))
            {
                return this;
            }
            return ThrowInvalidCast(conversionType);
        }

        private T ThrowInvalidCast<T>()
        {
            return (T)ThrowInvalidCast(typeof(T));
        }

        private object ThrowInvalidCast(Type type)
        {
            throw new InvalidCastException($"RemoteCancellationToken does not cast {type.FullName}.");
        }
    }

    public static class RemoteCancellationTokenExtensions
    {
        public static CancellationToken AsToken(this RemoteCancellationToken token)
        {
            return RemoteCancellationToken.AsToken(token);
        }
    }
}
