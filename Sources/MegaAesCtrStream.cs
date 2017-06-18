using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Misho.Cloud.MegaNz
{
    internal class MegaAesCtrStreamCrypter : MegaAesCtrStream
    {
        public MegaAesCtrStreamCrypter(Stream stream)
          : base(stream, stream.Length, Mode.Crypt, Crypto.CreateAesKey(), Crypto.CreateAesKey().CopySubArray(8))
        {
        }

        public byte[] FileKey
        {
            get { return fileKey; }
        }

        public byte[] Iv
        {
            get { return iv; }
        }

        public byte[] MetaMac
        {
            get
            {
                if (position != streamLength)
                {
                    throw new NotSupportedException("Stream must be fully read to obtain computed FileMac");
                }

                return metaMac;
            }
        }
    }

    internal class MegaAesCtrStreamDecrypter : MegaAesCtrStream
    {
        private readonly byte[] expectedMetaMac;

        public MegaAesCtrStreamDecrypter(Stream stream, long streamLength, byte[] fileKey, byte[] iv, byte[] expectedMetaMac)
          : base(stream, streamLength, Mode.Decrypt, fileKey, iv)
        {
            if (expectedMetaMac == null || expectedMetaMac.Length != 8)
            {
                throw new ArgumentException("Invalid expectedMetaMac");
            }

            this.expectedMetaMac = expectedMetaMac;
        }

        protected override void OnStreamRead()
        {
            if (!expectedMetaMac.SequenceEqual(metaMac))
            {
                throw new DownloadException();
            }
        }
    }

    internal abstract class MegaAesCtrStream : Stream
    {
        protected readonly byte[] fileKey;
        protected readonly byte[] iv;
        protected readonly long streamLength;
        protected long position = 0;
        protected byte[] metaMac = new byte[8];

        private readonly Stream stream;
        private readonly Mode mode;
        private readonly long[] chunksPositions;
        private readonly byte[] counter = new byte[8];
        private long currentCounter = 0;
        private byte[] currentChunkMac = new byte[16];
        private byte[] fileMac = new byte[16];

        protected MegaAesCtrStream(Stream stream, long streamLength, Mode mode, byte[] fileKey, byte[] iv)
        {
            if (stream == null)
            {
                throw new ArgumentNullException("stream");
            }

            if (fileKey == null || fileKey.Length != 16)
            {
                throw new ArgumentException("Invalid fileKey");
            }

            if (iv == null || iv.Length != 8)
            {
                throw new ArgumentException("Invalid Iv");
            }

            this.stream = stream;
            this.streamLength = streamLength;
            this.mode = mode;
            this.fileKey = fileKey;
            this.iv = iv;

            chunksPositions = GetChunksPositions(this.streamLength);
        }

        protected enum Mode
        {
            Crypt,
            Decrypt
        }

        public long[] ChunksPositions
        {
            get { return chunksPositions; }
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public override long Length
        {
            get { return streamLength; }
        }

        public override long Position
        {
            get
            {
                return position;
            }

            set
            {
                if (position != value)
                {
                    throw new NotSupportedException("Seek is not supported");
                }
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (position == streamLength)
            {
                return 0;
            }

            for (long pos = position; pos < Math.Min(position + count, streamLength); pos += 16)
            {
                // We are on a chunk bondary
                if (chunksPositions.Any(chunk => chunk == pos))
                {
                    if (pos != 0)
                    {
                        // Compute the current chunk mac data on each chunk bondary
                        ComputeChunk();
                    }

                    // Init chunk mac with Iv values
                    for (int i = 0; i < 8; i++)
                    {
                        currentChunkMac[i] = iv[i];
                        currentChunkMac[i + 8] = iv[i];
                    }
                }

                IncrementCounter();

                // Iterate each AES 16 bytes block
                byte[] input = new byte[16];
                byte[] output = new byte[input.Length];
                int inputLength = stream.Read(input, 0, input.Length);
                if (inputLength != input.Length)
                {
                    // Sometimes, the stream is not finished but the read is not complete
                    inputLength += stream.Read(input, inputLength, input.Length - inputLength);
                }

                // Merge Iv and counter
                byte[] ivCounter = new byte[16];
                Array.Copy(iv, ivCounter, 8);
                Array.Copy(counter, 0, ivCounter, 8, 8);

                byte[] encryptedIvCounter = Crypto.EncryptAes(ivCounter, fileKey);

                for (int inputPos = 0; inputPos < inputLength; inputPos++)
                {
                    output[inputPos] = (byte)(encryptedIvCounter[inputPos] ^ input[inputPos]);
                    currentChunkMac[inputPos] ^= (mode == Mode.Crypt) ? input[inputPos] : output[inputPos];
                }

                // Copy to buffer
                Array.Copy(output, 0, buffer, offset + pos - position, Math.Min(output.Length, streamLength - pos));

                // Crypt to current chunk mac
                currentChunkMac = Crypto.EncryptAes(currentChunkMac, fileKey);
            }

            long len = Math.Min(count, streamLength - position);
            position += len;

            // When stream is fully processed, we compute the last chunk
            if (position == streamLength)
            {
                ComputeChunk();

                // Compute Meta MAC
                for (int i = 0; i < 4; i++)
                {
                    metaMac[i] = (byte)(fileMac[i] ^ fileMac[i + 4]);
                    metaMac[i + 4] = (byte)(fileMac[i + 8] ^ fileMac[i + 12]);
                }

                OnStreamRead();
            }

            return (int)len;
        }

        public override void Flush()
        {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        protected virtual void OnStreamRead()
        {
        }

        private void IncrementCounter()
        {
            byte[] counter = BitConverter.GetBytes(currentCounter++);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(counter);
            }

            Array.Copy(counter, this.counter, 8);
        }

        private void ComputeChunk()
        {
            for (int i = 0; i < 16; i++)
            {
                fileMac[i] ^= currentChunkMac[i];
            }

            fileMac = Crypto.EncryptAes(fileMac, fileKey);
        }

        private long[] GetChunksPositions(long size)
        {
            List<long> chunks = new List<long>();
            chunks.Add(0);

            long chunkStartPosition = 0;
            for (int idx = 1; (idx <= 8) && (chunkStartPosition < (size - (idx * 131072))); idx++)
            {
                chunkStartPosition += idx * 131072;
                chunks.Add(chunkStartPosition);
            }

            while ((chunkStartPosition + 1048576) < size)
            {
                chunkStartPosition += 1048576;
                chunks.Add(chunkStartPosition);
            }

            return chunks.ToArray();
        }
    }
}
