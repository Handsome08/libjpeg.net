using System;
using System.Collections.Generic;
using System.Diagnostics;

#if !NETSTANDARD
// namespace System.Drawing.* is not available in .NET Standard
using System.Drawing;
using System.Drawing.Imaging;
#endif

using System.IO;
using System.Runtime.InteropServices;
using BitMiracle.LibJpeg.Classic;

namespace BitMiracle.LibJpeg
{
    /// <summary>
    /// Main class for work with JPEG images.
    /// </summary>
#if EXPOSE_LIBJPEG
    public
#endif
    sealed class JpegImage : IDisposable
    {
        private bool m_alreadyDisposed;

        /// <summary>
        /// Description of image pixels (samples)
        /// </summary>
        private List<SampleRow> m_rows = new List<SampleRow>();

        private int m_width;
        private int m_height;
        private byte m_bitsPerComponent;
        private byte m_componentsPerSample;
        private Colorspace m_colorspace;

        // Fields below (m_compressedData, m_decompressedData, m_bitmap) are not initialized in constructors necessarily.
        // Instead direct access to these field you should use corresponding properties (compressedData, decompressedData, bitmap)
        // Such agreement allows to load required data (e.g. compress image) only by request.

        /// <summary>
        /// Bytes of jpeg image. Refreshed when m_compressionParameters changed.
        /// </summary>
        private MemoryStream m_compressedData;

        /// <summary>
        /// Current compression parameters corresponding with compressed data.
        /// </summary>
        private CompressionParameters m_compressionParameters;

        /// <summary>
        /// Bytes of decompressed image (bitmap)
        /// </summary>
        private MemoryStream m_decompressedData;

#if !NETSTANDARD
        /// <summary>
        /// .NET bitmap associated with this image
        /// </summary>
        private Bitmap m_bitmap;
#endif

#if !NETSTANDARD
        /// <summary>
        /// Creates <see cref="JpegImage"/> from <see cref="System.Drawing.Bitmap">.NET bitmap</see>
        /// </summary>
        /// <param name="bitmap">Source .NET bitmap.</param>
        public JpegImage(System.Drawing.Bitmap bitmap)
        {
            createFromBitmap(bitmap);
        }

        /// <summary>
        /// Creates <see cref="JpegImage"/> from file with an arbitrary image
        /// </summary>
        /// <param name="fileName">Path to file with image in 
        /// arbitrary format (BMP, Jpeg, GIF, PNG, TIFF, e.t.c)</param>
        public JpegImage(string fileName)
        {
            if (fileName == null)
                throw new ArgumentNullException("fileName");

            using (FileStream input = new FileStream(fileName, FileMode.Open))
                createFromStream(input);
        }
#endif

        /// <summary>
        /// Creates <see cref="JpegImage"/> from stream with an arbitrary image data
        /// </summary>
        /// <param name="imageData">Stream containing bytes of image in 
        /// arbitrary format (BMP, Jpeg, GIF, PNG, TIFF, e.t.c)</param>
        public JpegImage(Stream imageData)
        {
            createFromStream(imageData);
        }

        /// <summary>
        /// Creates <see cref="JpegImage"/> from pixels
        /// </summary>
        /// <param name="sampleData">Description of pixels.</param>
        /// <param name="colorspace">Colorspace of image.</param>
        /// <seealso cref="SampleRow"/>
        public JpegImage(SampleRow[] sampleData, Colorspace colorspace)
        {
            if (sampleData == null)
                throw new ArgumentNullException("sampleData");

            if (sampleData.Length == 0)
                throw new ArgumentException("sampleData must be no empty");

            if (colorspace == Colorspace.Unknown)
                throw new ArgumentException("Unknown colorspace");

            m_rows = new List<SampleRow>(sampleData);

            SampleRow firstRow = m_rows[0];
            m_width = firstRow.Length;
            m_height = m_rows.Count;

            Sample firstSample = firstRow[0];
            m_bitsPerComponent = firstSample.BitsPerComponent;
            m_componentsPerSample = firstSample.ComponentCount;
            m_colorspace = colorspace;
        }

#if !NETSTANDARD
        /// <summary>
        /// Creates <see cref="JpegImage"/> from <see cref="System.Drawing.Bitmap">.NET bitmap</see>
        /// </summary>
        /// <param name="bitmap">Source .NET bitmap.</param>
        /// <returns>Created instance of <see cref="JpegImage"/> class.</returns>
        /// <remarks>Same as corresponding <see cref="M:BitMiracle.LibJpeg.JpegImage.#ctor(System.Drawing.Bitmap)">constructor</see>.</remarks>
        public static JpegImage FromBitmap(Bitmap bitmap)
        {
            return new JpegImage(bitmap);
        }
#endif

        /// <summary>
        /// Frees and releases all resources allocated by this <see cref="JpegImage"/>
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!m_alreadyDisposed)
            {
                if (disposing)
                {
                    // dispose managed resources
                    if (m_compressedData != null)
                        m_compressedData.Dispose();

                    if (m_decompressedData != null)
                        m_decompressedData.Dispose();

#if !NETSTANDARD
                    if (m_bitmap != null)
                        m_bitmap.Dispose();
#endif
                }

                // free native resources
                m_compressionParameters = null;
                m_compressedData = null;
                m_decompressedData = null;
#if !NETSTANDARD                
                m_bitmap = null;
#endif
                m_rows = null;
                m_alreadyDisposed = true;
            }
        }

        /// <summary>
        /// Gets the width of image in <see cref="Sample">samples</see>.
        /// </summary>
        /// <value>The width of image.</value>
        public int Width
        {
            get
            {
                return m_width;
            }
            internal set
            {
                m_width = value;
            }
        }

        /// <summary>
        /// Gets the height of image in <see cref="Sample">samples</see>.
        /// </summary>
        /// <value>The height of image.</value>
        public int Height
        {
            get
            {
                return m_height;
            }
            internal set
            {
                m_height = value;
            }
        }

        /// <summary>
        /// Gets the number of color components per <see cref="Sample">sample</see>.
        /// </summary>
        /// <value>The number of color components per sample.</value>
        public byte ComponentsPerSample
        {
            get
            {
                return m_componentsPerSample;
            }
            internal set
            {
                m_componentsPerSample = value;
            }
        }

        /// <summary>
        /// Gets the number of bits per color component of <see cref="Sample">sample</see>.
        /// </summary>
        /// <value>The number of bits per color component.</value>
        public byte BitsPerComponent
        {
            get
            {
                return m_bitsPerComponent;
            }
            internal set
            {
                m_bitsPerComponent = value;
            }
        }

        /// <summary>
        /// Gets the colorspace of image.
        /// </summary>
        /// <value>The colorspace of image.</value>
        public Colorspace Colorspace
        {
            get
            {
                return m_colorspace;
            }
            internal set
            {
                m_colorspace = value;
            }
        }


        /// <summary>
        /// Retrieves the required row of image.
        /// </summary>
        /// <param name="rowNumber">The number of row.</param>
        /// <returns>Image row of samples.</returns>
        public SampleRow GetRow(int rowNumber)
        {
            return m_rows[rowNumber];
        }

        /// <summary>
        /// Writes compressed JPEG image to stream.
        /// </summary>
        /// <param name="output">Output stream.</param>
        public void WriteJpeg(Stream output)
        {
            WriteJpeg(output, new CompressionParameters());
        }

        /// <summary>
        /// Compresses image to JPEG with given parameters and writes it to stream.
        /// </summary>
        /// <param name="output">Output stream.</param>
        /// <param name="parameters">The parameters of compression.</param>
        public void WriteJpeg(Stream output, CompressionParameters parameters)
        {
            compress(parameters);
            compressedData.WriteTo(output);
        }

        /// <summary>
        /// Writes decompressed image data as bitmap to stream.
        /// </summary>
        /// <param name="output">Output stream.</param>
        public void WriteBitmap(Stream output)
        {
            decompressedData.WriteTo(output);
        }

#if !NETSTANDARD
        /// <summary>
        /// Retrieves image as .NET Bitmap.
        /// </summary>
        /// <returns>.NET Bitmap</returns>
        public Bitmap ToBitmap()
        {
            return bitmap.Clone() as Bitmap;
        }
#endif

        private MemoryStream compressedData
        {
            get
            {
                if (m_compressedData == null)
                    compress(new CompressionParameters());

                Debug.Assert(m_compressedData != null);
                Debug.Assert(m_compressedData.Length != 0);

                return m_compressedData;
            }
        }

        private MemoryStream decompressedData
        {
            get
            {
                if (m_decompressedData == null)
                    fillDecompressedData();

                Debug.Assert(m_decompressedData != null);

                return m_decompressedData;
            }
        }

#if !NETSTANDARD
        private Bitmap bitmap
        {
            get
            {
                if (m_bitmap == null)
                {
                    long position = compressedData.Position;
                    m_bitmap = new Bitmap(compressedData);
                    compressedData.Seek(position, SeekOrigin.Begin);
                }

                return m_bitmap;
            }
        }
#endif

        /// <summary>
        /// Needs for DecompressorToJpegImage class
        /// </summary>
        internal void addSampleRow(SampleRow row)
        {
            if (row == null)
                throw new ArgumentNullException("row");

            m_rows.Add(row);
        }

        /// <summary>
        /// Checks if imageData contains jpeg image
        /// </summary>
        private static bool isCompressed(Stream imageData)
        {
            if (imageData == null)
                return false;

            if (imageData.Length <= 2)
                return false;

            imageData.Seek(0, SeekOrigin.Begin);
            int first = imageData.ReadByte();
            int second = imageData.ReadByte();
            return (first == 0xFF && second == (int)JPEG_MARKER.SOI);
        }

        private void createFromStream(Stream imageData)
        {
            if (imageData == null)
                throw new ArgumentNullException("imageData");

            if (isCompressed(imageData))
            {
                m_compressedData = Utils.CopyStream(imageData);
                decompress();
            }
            else
            {
#if !NETSTANDARD
                createFromBitmap(new Bitmap(imageData));
#else
                throw new NotImplementedException("JpegImage.createFromStream(Stream)");
#endif
            }
        }

#if !NETSTANDARD
        private void createFromBitmap(System.Drawing.Bitmap bitmap)
        {
            initializeFromBitmap(bitmap);
            compress(new CompressionParameters());
        }

        private void initializeFromBitmap(Bitmap bitmap)
        {
            if (bitmap == null)
                throw new ArgumentNullException("bitmap");

            m_bitmap = bitmap;
            m_width = m_bitmap.Width;
            m_height = m_bitmap.Height;
            processPixelFormat(bitmap.PixelFormat);
            fillSamplesFromBitmap();
        }
#endif

        private void compress(CompressionParameters parameters)
        {
            Debug.Assert(m_rows != null);
            Debug.Assert(m_rows.Count != 0);

            RawImage source = new RawImage(m_rows, m_colorspace);
            compress(source, parameters);
        }

        private void compress(IRawImage source, CompressionParameters parameters)
        {
            Debug.Assert(source != null);

            if (!needCompressWith(parameters))
                return;

            m_compressedData = new MemoryStream();
            m_compressionParameters = new CompressionParameters(parameters);

            Jpeg jpeg = new Jpeg();
            jpeg.CompressionParameters = m_compressionParameters;
            jpeg.Compress(source, m_compressedData);
        }

        private bool needCompressWith(CompressionParameters parameters)
        {
            return m_compressedData == null || 
                   m_compressionParameters == null || 
                   !m_compressionParameters.Equals(parameters);
        }

        private void decompress()
        {
            if (TryInsertHuffmanTable(m_compressedData.GetBuffer(), (int) compressedData.Length, out var newData))
            {
                m_compressedData.Dispose();
                m_compressedData = new MemoryStream(newData);
            }
            Jpeg jpeg = new Jpeg();
            jpeg.Decompress(compressedData, new DecompressorToJpegImage(this));
        }

        private void fillDecompressedData()
        {
            Debug.Assert(m_decompressedData == null);

            m_decompressedData = new MemoryStream();
            BitmapDestination dest = new BitmapDestination(m_decompressedData);

            Jpeg jpeg = new Jpeg();
            jpeg.Decompress(compressedData, dest);
        }

#if !NETSTANDARD
        private void processPixelFormat(PixelFormat pixelFormat)
        {
            //See GdiPlusPixelFormats.h for details

            if (pixelFormat == PixelFormat.Format16bppGrayScale)
            {
                m_bitsPerComponent = 16;
                m_componentsPerSample = 1;
                m_colorspace = Colorspace.Grayscale;
                return;
            }

            byte formatIndexByte = (byte)((int)pixelFormat & 0x000000FF);
            byte pixelSizeByte = (byte)((int)pixelFormat & 0x0000FF00);

            if (pixelSizeByte == 32 && formatIndexByte == 15) //PixelFormat32bppCMYK (15 | (32 << 8))
            {
                m_bitsPerComponent = 8;
                m_componentsPerSample = 4;
                m_colorspace = Colorspace.CMYK;
                return;
            }

            m_bitsPerComponent = 8;
            m_componentsPerSample = 3;
            m_colorspace = Colorspace.RGB;
            
            if (pixelSizeByte == 16)
                m_bitsPerComponent = 6;
            else if (pixelSizeByte == 24 || pixelSizeByte == 32)
                m_bitsPerComponent = 8;
            else if (pixelSizeByte == 48 || pixelSizeByte == 64)
                m_bitsPerComponent = 16;
        }

        private void fillSamplesFromBitmap()
        {
            Debug.Assert(m_bitmap != null);

            for (int y = 0; y < Height; ++y)
            {
                short[] samples = new short[Width * 3];
                for (int x = 0; x < Width; ++x)
                {
                    Color color = m_bitmap.GetPixel(x, y);
                    samples[x * 3] = color.R;
                    samples[x * 3 + 1] = color.G;
                    samples[x * 3 + 2] = color.B;
                }
                m_rows.Add(new SampleRow(samples, m_bitsPerComponent, m_componentsPerSample));
            }
        }
#endif

        private static byte[] HuffmanTable = {
            0xff, 0xc4, 0x01, 0xa2, 0x00, 0x00, 0x01, 0x05, 0x01, 0x01, 0x01, 0x01,
            0x01, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x02,
            0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0a, 0x0b, 0x01, 0x00, 0x03,
            0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09,
            0x0a, 0x0b, 0x10, 0x00, 0x02, 0x01, 0x03, 0x03, 0x02, 0x04, 0x03, 0x05,
            0x05, 0x04, 0x04, 0x00, 0x00, 0x01, 0x7d, 0x01, 0x02, 0x03, 0x00, 0x04,
            0x11, 0x05, 0x12, 0x21, 0x31, 0x41, 0x06, 0x13, 0x51, 0x61, 0x07, 0x22,
            0x71, 0x14, 0x32, 0x81, 0x91, 0xa1, 0x08, 0x23, 0x42, 0xb1, 0xc1, 0x15,
            0x52, 0xd1, 0xf0, 0x24, 0x33, 0x62, 0x72, 0x82, 0x09, 0x0a, 0x16, 0x17,
            0x18, 0x19, 0x1a, 0x25, 0x26, 0x27, 0x28, 0x29, 0x2a, 0x34, 0x35, 0x36,
            0x37, 0x38, 0x39, 0x3a, 0x43, 0x44, 0x45, 0x46, 0x47, 0x48, 0x49, 0x4a,
            0x53, 0x54, 0x55, 0x56, 0x57, 0x58, 0x59, 0x5a, 0x63, 0x64, 0x65, 0x66,
            0x67, 0x68, 0x69, 0x6a, 0x73, 0x74, 0x75, 0x76, 0x77, 0x78, 0x79, 0x7a,
            0x83, 0x84, 0x85, 0x86, 0x87, 0x88, 0x89, 0x8a, 0x92, 0x93, 0x94, 0x95,
            0x96, 0x97, 0x98, 0x99, 0x9a, 0xa2, 0xa3, 0xa4, 0xa5, 0xa6, 0xa7, 0xa8,
            0xa9, 0xaa, 0xb2, 0xb3, 0xb4, 0xb5, 0xb6, 0xb7, 0xb8, 0xb9, 0xba, 0xc2,
            0xc3, 0xc4, 0xc5, 0xc6, 0xc7, 0xc8, 0xc9, 0xca, 0xd2, 0xd3, 0xd4, 0xd5,
            0xd6, 0xd7, 0xd8, 0xd9, 0xda, 0xe1, 0xe2, 0xe3, 0xe4, 0xe5, 0xe6, 0xe7,
            0xe8, 0xe9, 0xea, 0xf1, 0xf2, 0xf3, 0xf4, 0xf5, 0xf6, 0xf7, 0xf8, 0xf9,
            0xfa, 0x11, 0x00, 0x02, 0x01, 0x02, 0x04, 0x04, 0x03, 0x04, 0x07, 0x05,
            0x04, 0x04, 0x00, 0x01, 0x02, 0x77, 0x00, 0x01, 0x02, 0x03, 0x11, 0x04,
            0x05, 0x21, 0x31, 0x06, 0x12, 0x41, 0x51, 0x07, 0x61, 0x71, 0x13, 0x22,
            0x32, 0x81, 0x08, 0x14, 0x42, 0x91, 0xa1, 0xb1, 0xc1, 0x09, 0x23, 0x33,
            0x52, 0xf0, 0x15, 0x62, 0x72, 0xd1, 0x0a, 0x16, 0x24, 0x34, 0xe1, 0x25,
            0xf1, 0x17, 0x18, 0x19, 0x1a, 0x26, 0x27, 0x28, 0x29, 0x2a, 0x35, 0x36,
            0x37, 0x38, 0x39, 0x3a, 0x43, 0x44, 0x45, 0x46, 0x47, 0x48, 0x49, 0x4a,
            0x53, 0x54, 0x55, 0x56, 0x57, 0x58, 0x59, 0x5a, 0x63, 0x64, 0x65, 0x66,
            0x67, 0x68, 0x69, 0x6a, 0x73, 0x74, 0x75, 0x76, 0x77, 0x78, 0x79, 0x7a,
            0x82, 0x83, 0x84, 0x85, 0x86, 0x87, 0x88, 0x89, 0x8a, 0x92, 0x93, 0x94,
            0x95, 0x96, 0x97, 0x98, 0x99, 0x9a, 0xa2, 0xa3, 0xa4, 0xa5, 0xa6, 0xa7,
            0xa8, 0xa9, 0xaa, 0xb2, 0xb3, 0xb4, 0xb5, 0xb6, 0xb7, 0xb8, 0xb9, 0xba,
            0xc2, 0xc3, 0xc4, 0xc5, 0xc6, 0xc7, 0xc8, 0xc9, 0xca, 0xd2, 0xd3, 0xd4,
            0xd5, 0xd6, 0xd7, 0xd8, 0xd9, 0xda, 0xe2, 0xe3, 0xe4, 0xe5, 0xe6, 0xe7,
            0xe8, 0xe9, 0xea, 0xf2, 0xf3, 0xf4, 0xf5, 0xf6, 0xf7, 0xf8, 0xf9, 0xfa
        };

        private static unsafe bool TryInsertHuffmanTable(byte[] data, int bufferLen, out byte[] newData)
        {
            var index = -1;
            bool containsHuff = false;
            for (int i = 0; i < bufferLen; i++)
            {
                if (data[i] == 0xff && data[i + 1] == 0xc4)
                {
                    containsHuff = true;
                    break;
                }
                if (data[i] == 0xff && data[i + 1] == 0xc0)
                {
                    index = i;
                    break;
                }
            }

            if (containsHuff)
            {
                newData = data;
                return false;
            }

            var dataWithHuff = new byte[bufferLen + HuffmanTable.Length];
            fixed (byte* huf = HuffmanTable)
            {
                fixed (byte* dst = dataWithHuff)
                {
                    fixed (byte* src = data)
                    {
                        memcpy(dst, src, index);
                        memcpy(dst + index, huf, HuffmanTable.Length);
                        memcpy(dst + index + HuffmanTable.Length, src + index, bufferLen - index);
                    }
                }
            }

            newData = dataWithHuff;
            return true;
        }

        [DllImport("ntdll.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static unsafe extern int memcpy(
            byte* dst,
            byte* src,
            int count);
    }
}
