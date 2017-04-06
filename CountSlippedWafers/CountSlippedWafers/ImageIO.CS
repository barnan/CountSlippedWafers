using System;
using System.Collections.Generic;
using Emgu.CV;
using Emgu.CV.Structure;
using System.Windows.Media.Imaging;     // PNG support  (NEED TO ADD PresentationCore from .NET to the project references)
using BitMiracle.LibTiff.Classic;
using System.IO;

namespace ImViewer.MyUtilities
{
    public class ImageIO
    {

        public class ImageContainer
        {
            public Image<Gray, double> doubleImage;
            public Image<Gray, float> floatImage;
            public Image<Gray, ushort> ushortImage;
            public Image<Gray, byte> byteImage;
            public int bitPix;
            public int imageWidth;
            public int imageHeight;
        }


        public static ImageContainer FITSLoad(string fileName)
        {
            int width, height, bitPix;
            byte[] pixelBuf = GetFITSImageBytes(fileName, out bitPix, out width, out height);

            ImageContainer emguImage = new ImageContainer();
            emguImage.imageWidth = width;
            emguImage.imageHeight = height;

            switch (bitPix)
            {
                case 8:

                    emguImage.byteImage = new Image<Gray, Byte>(width, height);
                    emguImage.byteImage.Bytes = pixelBuf;
                    emguImage.bitPix = bitPix;
                    return emguImage;

                case 16:

                    emguImage.ushortImage = new Image<Gray, ushort>(width, height);
                    emguImage.ushortImage.Bytes = pixelBuf;
                    emguImage.bitPix = bitPix;
                    return emguImage;

                case -32:

                    emguImage.floatImage = new Image<Gray, float>(width, height);
                    emguImage.floatImage.Bytes = pixelBuf;
                    emguImage.bitPix = bitPix;
                    return emguImage;

                case -64:

                    emguImage.doubleImage = new Image<Gray, double>(width, height);
                    emguImage.doubleImage.Bytes = pixelBuf;
                    emguImage.bitPix = bitPix;
                    return emguImage;

                default:
                    throw new Exception("Invalid BITPIX: " + bitPix);
            }
        }


        public static ImageContainer PNGLoad(string fileName)
        {
            int width, height, bitPix;
            byte[] pixelBuf = GetPNGImageBytes(fileName, out bitPix, out width, out height);

            ImageContainer emguImage = new ImageContainer();
            emguImage.imageWidth = width;
            emguImage.imageHeight = height;

            switch (bitPix)
            {
                case 8:

                    emguImage.byteImage = new Image<Gray, byte>(width, height);
                    emguImage.byteImage.Bytes = pixelBuf;
                    emguImage.bitPix = bitPix;
                    return emguImage;

                case 16:

                    emguImage.ushortImage = new Image<Gray, ushort>(width, height);
                    emguImage.ushortImage.Bytes = pixelBuf;
                    emguImage.bitPix = bitPix;
                    return emguImage;

                default:
                    throw new Exception("Invalid BITPIX: " + bitPix);
            }
        }



        private static byte[] GetFITSImageBytes(string fileName, out int bitPix, out int width, out int height)
        {
            byte[] pixelBuf = null;
            width = 0;
            height = 0;
            Dictionary<string, object> header = new Dictionary<string, object>();
            bitPix = 0;
            double bScale = 0;
            double bZero = 0;

            // Make sure we have a file
            if (!System.IO.File.Exists(fileName))
            {
                throw new FileNotFoundException(fileName);
            }

            try
            {
                // Open the file in binary mode
                using (System.IO.FileStream stream = new System.IO.FileStream(fileName, System.IO.FileMode.Open))
                {

                    // FITS header cards are always 80 bytes long and they come in gorups of 36!
                    byte[] buffer = new byte[81];

                    // Buffer cards are not zero terminated strings but fixed length (80). Add a 0 to terminate it.
                    buffer[80] = 0;

                    bool headerFlag = true;
                    int headerCount = 0;

                    // Read the header. Header has integer times 36 lines (blocks). Last block has an END card somewhere
                    while (headerFlag)
                    {
                        stream.Read(buffer, 0, 80);
                        headerCount++;
                        string strBuf = System.Text.Encoding.UTF8.GetString(buffer);

                        // Comments in cards start at the / caharacter. Trim it.
                        int index = strBuf.IndexOf('/');
                        if (0 < index)
                            strBuf = strBuf.Substring(0, index);

                        // Get the keyword, first 8 column. Trim trailing spaces
                        string keyword = strBuf.Substring(0, 8).TrimEnd(' ');

                        // Get the value. Starts in column 10 (0-7 is keyword, 8-9 contains an = sign)
                        string valueString = strBuf.Substring(10).TrimStart(' ').TrimEnd(' ');
                        object val = null;

                        // Value can be bool (T or F), string (enclosed in "") or int/float number

                        // String is enclosed in quotes
                        if (valueString[0] == '\"')
                        {
                            val = valueString.Substring(1, valueString.Length - 2);
                        }

                        // Bool is T or F
                        if (valueString.ToUpper() == "T")
                        {
                            val = true;
                        }
                        if (valueString.ToUpper() == "F")
                        {
                            val = false;
                        }

                        // Int and Float is parsed to double
                        double doubleVal;
                        if (double.TryParse(valueString, out doubleVal) && val == null)
                        {
                            val = doubleVal;
                        }

                        // add to dictionary
                        if (val != null)
                            header.Add(keyword, val);

                        // Check for known, important cards

                        // END signals the last block
                        if (keyword == "END")
                            headerFlag = false;

                        // BITPIX is the format
                        if (keyword == "BITPIX")
                            bitPix = Convert.ToInt32(val);

                        if (keyword == "NAXIS")
                        {
                            int nAxis = Convert.ToInt32(val);
                            if (nAxis != 2)
                                throw new Exception("Only 2D FITS files are supported");
                        }
                        if (keyword == "NAXIS1")
                        {
                            width = Convert.ToInt32(val);
                        }
                        if (keyword == "NAXIS2")
                        {
                            height = Convert.ToInt32(val);
                        }

                        if (keyword == "BSCALE")
                            bScale = (double)val;
                        if (keyword == "BZERO")
                            bZero = (double)val;

                    }

                    // Read until end of block
                    while (headerCount % 36 != 0)
                    {
                        stream.Read(buffer, 0, 80);
                        headerCount++;
                    }

                    // Allocate storage;
                    int pixelStorageSize = Math.Abs(bitPix) / 8;

                    int len = width * height * pixelStorageSize;
                    byte[] res = new byte[len];
                    stream.Read(res, 0, res.Length);

                    byte work;

                    if (pixelStorageSize == 2)
                    {
                        for (int i = 0; i < len; i += 2)
                        {
                            work = res[i];
                            res[i] = res[i + 1];
                            res[i + 1] = work;
                        }
                    }

                    if (pixelStorageSize == 4)
                    {
                        for (int i = 0; i < len; i += 4)
                        {
                            work = res[i];
                            res[i] = res[i + 3];
                            res[i + 3] = work;

                            work = res[i + 1];
                            res[i + 1] = res[i + 2];
                            res[i + 2] = work;
                        }
                    }

                    if (pixelStorageSize == 8)
                    {
                        for (int i = 0; i < len; i += 8)
                        {
                            work = res[i];
                            res[i] = res[i + 7];
                            res[i + 7] = work;

                            work = res[i + 1];
                            res[i + 1] = res[i + 6];
                            res[i + 6] = work;

                            work = res[i + 2];
                            res[i + 2] = res[i + 5];
                            res[i + 5] = work;

                            work = res[i + 3];
                            res[i + 3] = res[i + 4];
                            res[i + 4] = work;
                        }
                    }

                    int count = width * height;

                    pixelBuf = res;
                }
                return pixelBuf;
            }
            catch (System.Exception)
            {
                throw;
            }
        }


        private static byte[] GetPNGImageBytes(string fileName, out int bitPix, out int width, out int height)
        {
            width = 0;
            height = 0;
            string tempfileName = fileName;
            byte[] pixelBuf = null;
            bitPix = 0;

            using (Stream imageStreamSource = new FileStream(tempfileName, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                PngBitmapDecoder decoder = new PngBitmapDecoder(imageStreamSource, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default);
                BitmapSource bitmapSource = decoder.Frames[0];

                if (bitmapSource.Format == System.Windows.Media.PixelFormats.Gray16)
                    bitPix = 16;
                else if (bitmapSource.Format == System.Windows.Media.PixelFormats.Gray8)
                    bitPix = 8;
                else
                    throw new Exception("Expected bitPix = 8 or 16 (Byte or UInt16 pixel values)");


                int pixelStorageSize = Math.Abs(bitPix) / 8;

                width = bitmapSource.PixelWidth;
                height = bitmapSource.PixelHeight;

                int stride = width * pixelStorageSize;
                pixelBuf = new byte[height * width * pixelStorageSize];
                bitmapSource.CopyPixels(pixelBuf, stride, 0);
            }
            return pixelBuf;

        }


        public static void SaveFITS(float[][] pixels, string filename)
        {
            int width = pixels[0].Length;
            int height = pixels.Length;
            int stride = width * 4;
            byte[] pixelbytes = new byte[height * stride];
            string headerCard;
            System.Text.UTF8Encoding enc = new System.Text.UTF8Encoding();
            int cardCounter = 0;

            // Create a vector representation of the data
            if (!BitConverter.IsLittleEndian)
            {
                float[] fpixels = new float[width];
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        fpixels[x] = pixels[y][x];
                    }
                    Buffer.BlockCopy(fpixels, 0, pixelbytes, y * stride, stride);
                }
            }
            else
            {
                int cnt = 0;
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        byte[] bytes = BitConverter.GetBytes(pixels[y][x]);

                        pixelbytes[cnt++] = bytes[3];
                        pixelbytes[cnt++] = bytes[2];
                        pixelbytes[cnt++] = bytes[1];
                        pixelbytes[cnt++] = bytes[0];
                    }
                }
            }

            System.IO.FileStream stream = new System.IO.FileStream(filename, System.IO.FileMode.Create);

            /*This is to verify the header format in FITS, which is very strict
            Console.WriteLine("");
            Console.Write("         1");
            Console.Write("         2");
            Console.Write("         3");
            Console.Write("         4");
            Console.Write("         5");
            Console.Write("         6");
            Console.Write("         7");
            Console.WriteLine("         ");
            Console.Write("1234567890");
            Console.Write("1234567890");
            Console.Write("1234567890");
            Console.Write("1234567890");
            Console.Write("1234567890");
            Console.Write("1234567890");
            Console.Write("1234567890");
            Console.WriteLine("123456789");
            // */

            // SIMPLE = T
            headerCard = String.Format("{0,-8}= {1,20} / {2,-47}", "SIMPLE", "T", "file conforms to FITS standard");
            stream.Write(enc.GetBytes(headerCard), 0, 80);
            cardCounter++;

            // BITPIX = 16 as we ONLY deal with 16 bit data, FITS allows many values
            headerCard = String.Format("{0,-8}= {1,20} / {2,-47}", "BITPIX", -32, "number of bits per data pixel");
            stream.Write(enc.GetBytes(headerCard), 0, 80);
            cardCounter++;

            // NAXIS = 2 as we only deal with 2D images, FITS allows any dimension
            headerCard = String.Format("{0,-8}= {1,20} / {2,-47}", "NAXIS", 2, "number of data axes");
            stream.Write(enc.GetBytes(headerCard), 0, 80);
            cardCounter++;

            // NAXIS1 = width
            headerCard = String.Format("{0,-8}= {1,20} / {2,-47}", "NAXIS1", width, "length of data axis 1");
            stream.Write(enc.GetBytes(headerCard), 0, 80);
            cardCounter++;

            // NAXIS = 2 as we only deal with 2D images, FITS allows any dimension
            headerCard = String.Format("{0,-8}= {1,20} / {2,-47}", "NAXIS2", height, "length of data axis 2");
            stream.Write(enc.GetBytes(headerCard), 0, 80);
            cardCounter++;

            headerCard = String.Format("{0,-80}", "END");
            stream.Write(enc.GetBytes(headerCard), 0, 80);
            cardCounter++;

            headerCard = String.Format("{0,-80}", "");

            while (0 != (cardCounter % 36))
            {
                stream.Write(enc.GetBytes(headerCard), 0, 80);
                cardCounter++;
            }

            stream.Write(pixelbytes, 0, height * width * 4);

            stream.Close();
        }

        public static void SaveFITS(Image<Gray, double> image, string filename)
        {
            SaveFITS(image.Data, filename);
        }

        public static void SaveFITS(double[,,] pixels, string filename)
        {
            int width = pixels.GetLength(1);
            int height = pixels.GetLength(0);
            int stride = width * 8;
            byte[] pixelbytes = new byte[height * stride];
            string headerCard;
            System.Text.UTF8Encoding enc = new System.Text.UTF8Encoding();
            int cardCounter = 0;

            // Create a vector representation of the data
            if (!BitConverter.IsLittleEndian)
            {
                double[] fpixels = new double[width];
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        fpixels[x] = pixels[y, x, 0];
                    }
                    Buffer.BlockCopy(fpixels, 0, pixelbytes, y * stride, stride);
                }
            }
            else
            {
                int cnt = 0;
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        byte[] bytes = BitConverter.GetBytes(pixels[y, x, 0]);

                        pixelbytes[cnt++] = bytes[7];
                        pixelbytes[cnt++] = bytes[6];
                        pixelbytes[cnt++] = bytes[5];
                        pixelbytes[cnt++] = bytes[4];
                        pixelbytes[cnt++] = bytes[3];
                        pixelbytes[cnt++] = bytes[2];
                        pixelbytes[cnt++] = bytes[1];
                        pixelbytes[cnt++] = bytes[0];
                    }
                }
            }

            System.IO.FileStream stream = new System.IO.FileStream(filename, System.IO.FileMode.Create);

            /*This is to verify the header format in FITS, which is very strict
            Console.WriteLine("");
            Console.Write("         1");
            Console.Write("         2");
            Console.Write("         3");
            Console.Write("         4");
            Console.Write("         5");
            Console.Write("         6");
            Console.Write("         7");
            Console.WriteLine("         ");
            Console.Write("1234567890");
            Console.Write("1234567890");
            Console.Write("1234567890");
            Console.Write("1234567890");
            Console.Write("1234567890");
            Console.Write("1234567890");
            Console.Write("1234567890");
            Console.WriteLine("123456789");
            // */

            // SIMPLE = T
            headerCard = String.Format("{0,-8}= {1,20} / {2,-47}", "SIMPLE", "T", "file conforms to FITS standard");
            stream.Write(enc.GetBytes(headerCard), 0, 80);
            cardCounter++;

            // BITPIX = 16 as we ONLY deal with 16 bit data, FITS allows many values
            headerCard = String.Format("{0,-8}= {1,20} / {2,-47}", "BITPIX", -64, "number of bits per data pixel");
            stream.Write(enc.GetBytes(headerCard), 0, 80);
            cardCounter++;

            // NAXIS = 2 as we only deal with 2D images, FITS allows any dimension
            headerCard = String.Format("{0,-8}= {1,20} / {2,-47}", "NAXIS", 2, "number of data axes");
            stream.Write(enc.GetBytes(headerCard), 0, 80);
            cardCounter++;

            // NAXIS1 = width
            headerCard = String.Format("{0,-8}= {1,20} / {2,-47}", "NAXIS1", width, "length of data axis 1");
            stream.Write(enc.GetBytes(headerCard), 0, 80);
            cardCounter++;

            // NAXIS = 2 as we only deal with 2D images, FITS allows any dimension
            headerCard = String.Format("{0,-8}= {1,20} / {2,-47}", "NAXIS2", height, "length of data axis 2");
            stream.Write(enc.GetBytes(headerCard), 0, 80);
            cardCounter++;

            headerCard = String.Format("{0,-80}", "END");
            stream.Write(enc.GetBytes(headerCard), 0, 80);
            cardCounter++;

            headerCard = String.Format("{0,-80}", "");

            while (0 != (cardCounter % 36))
            {
                stream.Write(enc.GetBytes(headerCard), 0, 80);
                cardCounter++;
            }

            stream.Write(pixelbytes, 0, height * width * 8);

            stream.Close();
        }


        //this needs: using BitMiracle.LibTiff.Classic;
        public static ImageContainer TIFFLoad(string file)
        {

            // Open the file
            Tiff tiffImage = Tiff.Open(file, "r");

            // Extract the width
            var tmp = tiffImage.GetField(TiffTag.IMAGEWIDTH)[0].Value as int?;
            if (null == tmp)
            {
                throw new Exception("WIDTH ERROR");
            }

            // Extract the height
            int width = (int)tmp;
            tmp = tiffImage.GetField(TiffTag.IMAGELENGTH)[0].Value as int?;
            if (null == tmp)
            {
                throw new Exception("HEIGHT ERROR");
            }
            int height = (int)tmp;

            //image = new double[height][];
            Image<Gray, double> image = new Image<Gray, double>(width, height);

            // Get the depth
            var x = tiffImage.GetField(TiffTag.BITSPERSAMPLE)[0].Value;
            tmp = tiffImage.GetField(TiffTag.BITSPERSAMPLE)[0].Value as short?;
            if (null == tmp)
            {
                throw new Exception("DEPTH ERROR");
            }
            int depth = (int)tmp;
            if (depth != 32)
            {
                throw new Exception("Image depth is invalid: " + depth.ToString());
            }

            // Make sure we have a single image in the file
            if (tiffImage.NumberOfDirectories() != 1)
            {
                throw new Exception("Number of directories is wrong: " + tiffImage.NumberOfDirectories());
            }

            if (!tiffImage.IsBigEndian())
            {
                throw new Exception("Little endian");
            }

            SampleFormat sampleFormat = (SampleFormat)tiffImage.GetField(TiffTag.SAMPLEFORMAT)[0].Value;

            // Get the rows
            int scanLineSize = tiffImage.ScanlineSize();
            byte[] buffer = new byte[scanLineSize];

            for (int row = 0; row < height; row++)
            {
                tiffImage.ReadScanline(buffer, row);
                //image[row] = new double[width];
                for (int column = 0; column < width; column++)
                {
                    switch (sampleFormat)
                    {
                        case SampleFormat.IEEEFP:
                            //image[row][column] = BitConverter.ToSingle(buffer, column * 4);
                            image.Data[row, column, 0] = BitConverter.ToSingle(buffer, column * 4);
                            break;
                        default:
                            throw new Exception("Invalid sample format: " + sampleFormat.ToString());
                    }
                }
            }

            tiffImage.Close();

            ImageContainer emguImage = new ImageContainer();
            emguImage.doubleImage = image;
            emguImage.imageWidth = width;
            emguImage.imageHeight = height;
            emguImage.bitPix = -64; //? currently only 32 bit but we load it as double    

            return emguImage;
        }



        public static Image<Gray, double> LoadFITS(string fileName)
        {
            //this version creates Double images
            byte[] pixelBuf = null;
            int width = 0;
            int height = 0;
            Dictionary<string, object> header = new Dictionary<string, object>();
            int bitPix = 0;
            double bScale = 0;
            double bZero = 0;

            // Make sure we have a file
            if (!System.IO.File.Exists(fileName))
            {
                throw new FileNotFoundException(fileName);
            }

            try
            {
                // Open the file in binary mode
                using (System.IO.FileStream stream = new System.IO.FileStream(fileName, System.IO.FileMode.Open))
                {

                    // FITS header cards are always 80 bytes long and they come in gorups of 36!
                    byte[] buffer = new byte[81];

                    // Buffer cards are not zero terminated strings but fixed length (80). Add a 0 to terminate it.
                    buffer[80] = 0;

                    bool headerFlag = true;
                    int headerCount = 0;

                    // Read the header. Header has integer times 36 lines (blocks). Last block has an END card somewhere
                    while (headerFlag)
                    {
                        stream.Read(buffer, 0, 80);
                        headerCount++;
                        string strBuf = System.Text.Encoding.UTF8.GetString(buffer);

                        // Comments in cards start at the / caharacter. Trim it.
                        int index = strBuf.IndexOf('/');
                        if (0 < index)
                            strBuf = strBuf.Substring(0, index);

                        // Get the keyword, first 8 column. Trim trailing spaces
                        string keyword = strBuf.Substring(0, 8).TrimEnd(' ');

                        // Get the value. Starts in column 10 (0-7 is keyword, 8-9 contains an = sign)
                        string valueString = strBuf.Substring(10).TrimStart(' ').TrimEnd(' ');
                        object val = null;

                        // Value can be bool (T or F), string (enclosed in "") or int/float number

                        // String is enclosed in quotes
                        if (valueString[0] == '\"')
                        {
                            val = valueString.Substring(1, valueString.Length - 2);
                        }

                        // Bool is T or F
                        if (valueString.ToUpper() == "T")
                        {
                            val = true;
                        }
                        if (valueString.ToUpper() == "F")
                        {
                            val = false;
                        }

                        // Int and Float is parsed to double
                        double doubleVal;
                        if (double.TryParse(valueString, out doubleVal) && val == null)
                        {
                            val = doubleVal;
                        }

                        // add to dictionary
                        if (val != null)
                            header.Add(keyword, val);

                        // Check for known, important cards

                        // END signals the last block
                        if (keyword == "END")
                            headerFlag = false;

                        // BITPIX is the format
                        if (keyword == "BITPIX")
                            bitPix = Convert.ToInt32(val);

                        if (keyword == "NAXIS")
                        {
                            int nAxis = Convert.ToInt32(val);
                            if (nAxis != 2)
                                throw new Exception("Only 2D FITS files are supported");
                        }
                        if (keyword == "NAXIS1")
                        {
                            width = Convert.ToInt32(val);
                        }
                        if (keyword == "NAXIS2")
                        {
                            height = Convert.ToInt32(val);
                        }

                        if (keyword == "BSCALE")
                            bScale = (double)val;
                        if (keyword == "BZERO")
                            bZero = (double)val;

                    }

                    // Read until end of block
                    while (headerCount % 36 != 0)
                    {
                        stream.Read(buffer, 0, 80);
                        headerCount++;
                    }

                    // Allocate storage;
                    int pixelStorageSize = Math.Abs(bitPix) / 8;

                    int len = width * height * pixelStorageSize;
                    byte[] res = new byte[len];
                    stream.Read(res, 0, res.Length);

                    byte work;

                    if (pixelStorageSize == 2)
                    {
                        for (int i = 0; i < len; i += 2)
                        {
                            work = res[i];
                            res[i] = res[i + 1];
                            res[i + 1] = work;
                        }
                    }

                    if (pixelStorageSize == 4)
                    {
                        for (int i = 0; i < len; i += 4)
                        {
                            work = res[i];
                            res[i] = res[i + 3];
                            res[i + 3] = work;

                            work = res[i + 1];
                            res[i + 1] = res[i + 2];
                            res[i + 2] = work;
                        }
                    }

                    if (pixelStorageSize == 8)
                    {
                        for (int i = 0; i < len; i += 8)
                        {
                            work = res[i];
                            res[i] = res[i + 7];
                            res[i + 7] = work;

                            work = res[i + 1];
                            res[i + 1] = res[i + 6];
                            res[i + 6] = work;

                            work = res[i + 2];
                            res[i + 2] = res[i + 5];
                            res[i + 5] = work;

                            work = res[i + 3];
                            res[i + 3] = res[i + 4];
                            res[i + 4] = work;
                        }
                    }

                    int count = width * height;

                    pixelBuf = res;
                }


                var emguImage = new Image<Gray, double>(width, height);
                emguImage.Bytes = pixelBuf;
                return emguImage;
            }
            catch (System.Exception)
            {
                throw;
            }
        }

        public static Image<Gray, double> LoadPNG16(string fileName)
        {
            int width, height, bitPix;
            byte[] pixelBuf = GetPNGImageBytes(fileName, out bitPix, out width, out height);

            if (bitPix == 8)
            {
                Image<Gray, byte> emguImage = new Image<Gray, byte>(width, height);
                emguImage.Bytes = pixelBuf;
                return emguImage.Convert<Gray, double>();
            }

            if (bitPix == 16)
            {
                Image<Gray, ushort> emguImage = new Image<Gray, ushort>(width, height);
                emguImage.Bytes = pixelBuf;
                return emguImage.Convert<Gray,double>();
            }
            else
            {
                throw new Exception("Invalid BITPIX: " + bitPix);
            }
        }
    }
}
