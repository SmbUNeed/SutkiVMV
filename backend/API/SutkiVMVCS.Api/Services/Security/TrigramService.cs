using System.Buffers;
using System.Security.Cryptography;
using System.Text;

namespace SutkiVMVCS.Services.Security
{
    public class TrigramService
    {
        private readonly byte[] _hmacKey;
        public TrigramService(byte[] hmacKey) {
            _hmacKey = hmacKey;
        }
        public byte[] GenerateBloomFilter(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return Array.Empty<byte>();

            input = input.Trim().ToLowerInvariant();

            int charCount = input.Length;
            if (charCount < 3) return Array.Empty<byte>();

            int trigramCount = charCount - 2;

            byte[] bloomFilter = new byte[32];

            Span<byte> hashBuffer = stackalloc byte[32];

            Span<byte> trigramBytesBuffer = stackalloc byte[16];

            for (int i = 0; i < trigramCount; i++)
            {
                ReadOnlySpan<char> trigramChars = input.AsSpan(i, 3);
                int writtenBytes = Encoding.UTF8.GetBytes(trigramChars, trigramBytesBuffer);
                ReadOnlySpan<byte> trigramBytes = trigramBytesBuffer.Slice(0, writtenBytes);

                HMACSHA256.HashData(_hmacKey, trigramBytes, hashBuffer);

                SetBit(bloomFilter, hashBuffer.Slice(0, 4));
                SetBit(bloomFilter, hashBuffer.Slice(4, 4));
                SetBit(bloomFilter, hashBuffer.Slice(8, 4));
            }

            return bloomFilter;
        }
        private static void SetBit(byte[] filter, ReadOnlySpan<byte> hashChunk)
        {
            int intHash = BitConverter.ToInt32(hashChunk);

            int bitPosition = Math.Abs(intHash) % (32 * 8);

            int byteIndex = bitPosition / 8;
            int bitIndex = bitPosition % 8;

            filter[byteIndex] |= (byte)(1 << bitIndex);
        }
    }
}
