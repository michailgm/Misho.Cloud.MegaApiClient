using System.IO;
using System;

namespace Misho.Cloud.MegaNz
{
    internal class ProgressionStream : Stream
    {
        private readonly Stream baseStream;
        private readonly IProgress<double> progress;
        private readonly long reportProgressChunkSize;

        private long chunkSize;

        public ProgressionStream(Stream baseStream, IProgress<double> progress, long reportProgressChunkSize)
        {
            this.baseStream = baseStream;
            this.progress = progress;
            this.reportProgressChunkSize = reportProgressChunkSize;
        }

        public override int Read(byte[] array, int offset, int count)
        {
            int bytesRead = baseStream.Read(array, offset, count);
            ReportProgress(bytesRead);

            return bytesRead;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            baseStream.Write(buffer, offset, count);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            progress.Report(100);
        }

        public override void Flush()
        {
            baseStream.Flush();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return baseStream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            baseStream.SetLength(value);
        }

        public override bool CanRead => baseStream.CanRead;

        public override bool CanSeek => baseStream.CanSeek;

        public override bool CanWrite => baseStream.CanWrite;

        public override long Length => baseStream.Length;

        public override long Position
        {
            get { return baseStream.Position; }
            set { baseStream.Position = value; }
        }

        private void ReportProgress(int count)
        {
            chunkSize += count;
            if (chunkSize >= reportProgressChunkSize)
            {
                chunkSize = 0;
                progress.Report(Position / (double)Length * 100);
            }
        }
    }
}