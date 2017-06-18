using System;
using System.IO;
using System.Threading;

namespace Misho.Cloud.MegaNz
{
    public class CancellableStream : Stream
    {
        private Stream stream;
        private readonly CancellationToken cancellationToken;

        public CancellableStream(Stream stream, CancellationToken cancellationToken)
        {
            this.stream = stream ?? throw new ArgumentNullException(nameof(stream));
            this.cancellationToken = cancellationToken;
        }

        public override bool CanRead
        {
            get
            {
                cancellationToken.ThrowIfCancellationRequested();
                return stream.CanRead;
            }
        }

        public override bool CanSeek
        {
            get
            {
                cancellationToken.ThrowIfCancellationRequested();
                return stream.CanSeek;
            }
        }

        public override bool CanWrite
        {
            get
            {
                cancellationToken.ThrowIfCancellationRequested();
                return stream.CanWrite;
            }
        }

        public override void Flush()
        {
            cancellationToken.ThrowIfCancellationRequested();
            stream.Flush();
        }

        public override long Length
        {
            get
            {
                cancellationToken.ThrowIfCancellationRequested();
                return stream.Length;
            }
        }

        public override long Position
        {
            get
            {
                cancellationToken.ThrowIfCancellationRequested();
                return stream.Position;
            }

            set
            {
                cancellationToken.ThrowIfCancellationRequested();
                stream.Position = value;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return stream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return stream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            cancellationToken.ThrowIfCancellationRequested();
            stream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            cancellationToken.ThrowIfCancellationRequested();
            stream.Write(buffer, offset, count);
        }

        public override void Close()
        {
            stream?.Close();

            base.Close();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                stream?.Dispose();
                stream = null;
            }
        }
    }
}