using System;
using System.Threading;

namespace Persimmon.TestRunner.Internals
{
    public sealed class RemoteCancellationToken
#if !NETCORE
        : MarshalByRefObject
#endif
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
#if !NETCORE
            : MarshalByRefObject
#endif
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
    }

    public static class RemoteCancellationTokenExtensions
    {
        public static CancellationToken AsToken(this RemoteCancellationToken token)
        {
            return RemoteCancellationToken.AsToken(token);
        }
    }
}
