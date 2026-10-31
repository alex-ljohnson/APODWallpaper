using System.Buffers.Binary;

namespace APODWallpaper.Utils
{
    public enum ImageFormat { Jpeg, Png, Gif, Bmp, Tiff }

    public readonly record struct ImageSize(int Width, int Height);

    // Null Size when the header couldn't be read
    public sealed record ImageCheck(ImageFormat Format, ImageSize? Size, bool Undersized);

    public sealed record SavedImage(string Path, Uri Source);

    /// <summary>
    /// Checks from file content only, never the URL or Content-Type
    /// </summary>
    public static class ImageFile
    {
        private const int SignatureLength = 8;
        private const int MaxTiffEntries = 4096;

        private static ReadOnlySpan<byte> JpegSignature => [0xFF, 0xD8, 0xFF];
        private static ReadOnlySpan<byte> PngSignature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        private static ReadOnlySpan<byte> TiffLittleSignature => [0x49, 0x49, 0x2A, 0x00];
        private static ReadOnlySpan<byte> TiffBigSignature => [0x4D, 0x4D, 0x00, 0x2A];

        public static ImageFormat? Detect(ReadOnlySpan<byte> header)
        {
            if (header.StartsWith(JpegSignature)) return ImageFormat.Jpeg;
            if (header.StartsWith(PngSignature)) return ImageFormat.Png;
            if (header.StartsWith("GIF87a"u8) || header.StartsWith("GIF89a"u8)) return ImageFormat.Gif;
            if (header.StartsWith("BM"u8)) return ImageFormat.Bmp;
            if (header.StartsWith(TiffLittleSignature) || header.StartsWith(TiffBigSignature)) return ImageFormat.Tiff;
            return null;
        }

        public static ImageFormat? Detect(string path)
        {
            Span<byte> header = stackalloc byte[SignatureLength];
            using var file = File.OpenRead(path);
            int read = file.ReadAtLeast(header, header.Length, throwOnEndOfStream: false);
            return Detect(header[..read]);
        }

        public static string Extension(ImageFormat format) => format switch
        {
            ImageFormat.Jpeg => ".jpg",
            ImageFormat.Png => ".png",
            ImageFormat.Gif => ".gif",
            ImageFormat.Bmp => ".bmp",
            _ => ".tif",
        };

        public static ImageFormat? FromExtension(string extension) => extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => ImageFormat.Jpeg,
            ".png" => ImageFormat.Png,
            ".gif" => ImageFormat.Gif,
            ".bmp" => ImageFormat.Bmp,
            ".tif" or ".tiff" => ImageFormat.Tiff,
            _ => null,
        };

        /// <summary>
        /// Null when format isn't allowed, undersized is under half the expected width or height
        /// </summary>
        public static ImageCheck? Check(string path, ImageSize? expected)
        {
            using var file = File.OpenRead(path);
            var size = ReadSize(file, out var format);
            if (format is not ImageFormat found) return null;
            bool undersized = size is ImageSize s && expected is ImageSize e && (s.Width * 2 < e.Width || s.Height * 2 < e.Height);
            return new ImageCheck(found, size, undersized);
        }

        /// <summary>
        /// Size from the header without decoding, null when it can't be read, needs a seekable stream
        /// </summary>
        public static ImageSize? ReadSize(Stream image) => ReadSize(image, out _);

        private static ImageSize? ReadSize(Stream image, out ImageFormat? format)
        {
            Span<byte> header = stackalloc byte[SignatureLength];
            int read = ReadAt(image, 0, header, exact: false);
            format = Detect(header[..read]);
            try
            {
                var size = format switch
                {
                    ImageFormat.Jpeg => JpegSize(image),
                    ImageFormat.Png => PngSize(image),
                    ImageFormat.Gif => GifSize(image),
                    ImageFormat.Bmp => BmpSize(image),
                    ImageFormat.Tiff => TiffSize(image),
                    _ => null,
                };
                return size is { Width: > 0, Height: > 0 } ? size : null;
            }
            catch (EndOfStreamException)
            {
                return null;
            }
        }

        private static ImageSize? PngSize(Stream image)
        {
            Span<byte> ihdr = stackalloc byte[12];
            ReadAt(image, 12, ihdr);
            if (!ihdr.StartsWith("IHDR"u8)) return null;
            return new(BinaryPrimitives.ReadInt32BigEndian(ihdr[4..]), BinaryPrimitives.ReadInt32BigEndian(ihdr[8..]));
        }

        // Logical screen size
        private static ImageSize? GifSize(Stream image)
        {
            Span<byte> screen = stackalloc byte[4];
            ReadAt(image, 6, screen);
            return new(BinaryPrimitives.ReadUInt16LittleEndian(screen), BinaryPrimitives.ReadUInt16LittleEndian(screen[2..]));
        }

        // Height is negative for top-down bitmaps, 12 byte headers are the old OS/2 shape
        private static ImageSize? BmpSize(Stream image)
        {
            Span<byte> info = stackalloc byte[12];
            ReadAt(image, 14, info);
            if (BinaryPrimitives.ReadUInt32LittleEndian(info) == 12)
                return new(BinaryPrimitives.ReadUInt16LittleEndian(info[4..]), BinaryPrimitives.ReadUInt16LittleEndian(info[6..]));
            return new(BinaryPrimitives.ReadInt32LittleEndian(info[4..]), Math.Abs(BinaryPrimitives.ReadInt32LittleEndian(info[8..])));
        }

        // Walks segments to the first SOFn
        private static ImageSize? JpegSize(Stream image)
        {
            Span<byte> marker = stackalloc byte[4];
            Span<byte> frame = stackalloc byte[4];
            long position = 2;
            while (true)
            {
                ReadAt(image, position, marker);
                if (marker[0] != 0xFF) return null;
                byte type = marker[1];
                if (type == 0xFF) { position++; continue; }
                if (type is 0x01 or (>= 0xD0 and <= 0xD7)) { position += 2; continue; }
                if (type is 0xD9 or 0xDA) return null;
                if (type is >= 0xC0 and <= 0xCF and not 0xC4 and not 0xC8 and not 0xCC)
                {
                    // Length, precision, then height and width
                    ReadAt(image, position + 5, frame);
                    return new(BinaryPrimitives.ReadUInt16BigEndian(frame[2..]), BinaryPrimitives.ReadUInt16BigEndian(frame));
                }
                int length = BinaryPrimitives.ReadUInt16BigEndian(marker[2..]);
                if (length < 2) return null;
                position += 2 + length;
            }
        }

        // ImageWidth (256) and ImageLength (257) from the first directory, either byte order
        private static ImageSize? TiffSize(Stream image)
        {
            Span<byte> header = stackalloc byte[8];
            ReadAt(image, 0, header);
            bool little = header[0] == 0x49;
            uint U32(ReadOnlySpan<byte> b) => little ? BinaryPrimitives.ReadUInt32LittleEndian(b) : BinaryPrimitives.ReadUInt32BigEndian(b);
            ushort U16(ReadOnlySpan<byte> b) => little ? BinaryPrimitives.ReadUInt16LittleEndian(b) : BinaryPrimitives.ReadUInt16BigEndian(b);

            long directory = U32(header[4..]);
            Span<byte> count = stackalloc byte[2];
            ReadAt(image, directory, count);
            int entries = U16(count);
            if (entries > MaxTiffEntries) return null;

            int? width = null, height = null;
            Span<byte> entry = stackalloc byte[12];
            for (int i = 0; i < entries && (width == null || height == null); i++)
            {
                ReadAt(image, directory + 2 + i * 12L, entry);
                ushort tag = U16(entry), type = U16(entry[2..]);
                // SHORT or LONG
                int? value = type switch
                {
                    3 => U16(entry[8..]),
                    4 => (int)Math.Min(U32(entry[8..]), int.MaxValue),
                    _ => null,
                };
                if (tag == 256) width = value;
                else if (tag == 257) height = value;
            }
            return width is int w && height is int h ? new(w, h) : null;
        }

        private static int ReadAt(Stream stream, long offset, Span<byte> buffer, bool exact = true)
        {
            if (offset < 0 || offset >= stream.Length) return exact ? throw new EndOfStreamException() : 0;
            stream.Position = offset;
            return stream.ReadAtLeast(buffer, buffer.Length, throwOnEndOfStream: exact);
        }

        /// <summary>
        /// Saved image for the date with any allowed extension, null when there isn't one
        /// </summary>
        public static string? FindSaved(string imagesDirectory, DateOnly date)
        {
            if (!Directory.Exists(imagesDirectory)) return null;
            return Directory.EnumerateFiles(imagesDirectory, APODDate.ToIsoString(date) + ".*")
                .FirstOrDefault(f => Path.GetFileNameWithoutExtension(f) == APODDate.ToIsoString(date) && FromExtension(Path.GetExtension(f)) != null);
        }
    }
}
