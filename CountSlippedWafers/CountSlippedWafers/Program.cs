using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.UI;

namespace CountSlippedWafers
{
    class Program
    {
        private static MemStorage storage;
        static int _verticalThreshold = 10;
        static int _horizontalThreshold = 5;


        static void Main(string[] args)
        {
            #region allocate arrays, initialize variables:

            int width = 4096;
            int height = 8192;
            byte[] inputArray;
            byte[] array1 = new byte[height * width / 2];
            byte[] array2 = new byte[height * width / 2];

            Image<Gray, Byte> image1 = new Image<Gray, Byte>(width, height / 2);
            Image<Gray, Byte> image2 = new Image<Gray, Byte>(width, height / 2);

            Image<Gray, Byte> WaferMask;

            PointF[] contourPointfs;
            PointF[] AllContourPointfs = null;

            double[,] edgeVectors;

            string inputFolder = @"f:\_WORK\_SEMILAB\_SW_PROJECTS\CountSlippedWafers_Images";

            #endregion

            #region output file handling:

            string[] fileList = Directory.GetFiles(inputFolder);
            int[] resuList = new int[fileList.Length];

            if (File.Exists("Slippedwafers.csv"))
                File.Delete("Slippedwafers.csv");

            using (TextWriter tw = new StreamWriter("Slippedwafers.csv"))
            {
                tw.WriteLine("FileName;X;Y");
            }

            #endregion

            for (int m = 0; m < fileList.Length; m++)
            {

                using (storage = new MemStorage())
                {

                    #region read in image:
                    
                    inputArray = File.ReadAllBytes(fileList[m]);
                    AllContourPointfs = null;

                    for (int j = 0; j < height / 2; j++)
                    {
                        int rowWidth1 = j * 2 * width * 2;
                        int rowWidth2 = (j * 2 + 1) * width * 2;

                        for (int i = 0; i < width; i++)
                        {
                            int columnWidth = 2 * i;

                            array1[j * width + i] = (byte)(inputArray[rowWidth1 + columnWidth + 1] * 16); // + inputArray[rowWidth1 + columnWidth]);                    //its like div16 -> only the higher value byte is kept, the lower byte is skipped
                            array2[j * width + i] = (byte)(inputArray[rowWidth2 + columnWidth + 1] * 16); // + inputArray[rowWidth2 + columnWidth];
                        }
                    }

                    image1.Bytes = array1;
                    image1.Bytes = array2;

                    #endregion


                    #region image processing:

                    //int counter = 0;
                    WaferMask = image1.ThresholdBinary(new Gray(50), new Gray(255)).Convert<Gray, byte>();

                    for (Contour<Point> contours = WaferMask.FindContours(Emgu.CV.CvEnum.CHAIN_APPROX_METHOD.CV_CHAIN_APPROX_NONE, Emgu.CV.CvEnum.RETR_TYPE.CV_RETR_CCOMP, storage); contours != null; contours = contours.HNext)
                    {
                        Contour<Point> currentContour = new Contour<Point>(storage);

                        currentContour = contours;
                        image1.DrawPolyline(currentContour.ToArray(), true, new Gray(192), 3);

                        contourPointfs = Array.ConvertAll(contours.ToArray(), input => new PointF(input.X, input.Y));

                        if (AllContourPointfs == null || contourPointfs.Length > AllContourPointfs.Length)
                            AllContourPointfs = contourPointfs;

                        //string filename = $"{ Path.GetFileName(fileList[m])}_out.txt" + counter;
                        //using (TextWriter tw = new StreamWriter(filename))
                        //{
                        //    for (int l = 0; l < contourPointfs.Length; l++)
                        //        tw.WriteLine($"{l};{contourPointfs[l].X};{contourPointfs[l].Y}");
                        //}
                        //counter++;
                    }

                    edgeVectors = new double[AllContourPointfs.Length - 1, 4];

                    for (int i = 1; i < AllContourPointfs.Length - 2; i++)
                    {
                        edgeVectors[i, 0] = AllContourPointfs[i + 1].X - AllContourPointfs[i].X;
                        edgeVectors[i, 1] = AllContourPointfs[i + 1].Y - AllContourPointfs[i].Y;
                        edgeVectors[i, 2] = Math.Atan2(edgeVectors[i, 1], edgeVectors[i, 0]) * 180 / Math.PI;
                        edgeVectors[i, 3] = edgeVectors[i, 2] + edgeVectors[i - 1, 2];
                    }

                    //using (TextWriter tw = new StreamWriter($"{Path.GetFileName(fileList[m])}_edgeVectors.txt"))
                    //{
                    //    for (int i = 0; i < AllContourPointfs.Length - 2; i++)
                    //        tw.WriteLine($"{edgeVectors[i, 0]} ; {edgeVectors[i, 1]} ; {edgeVectors[i, 2]} ; {edgeVectors[i, 3]}");
                    //}

                    resuList[m] = EvalVectors(edgeVectors);

                    #endregion

                    //ImageViewer.Show(image1, "Image1");

                    #region save result to file:
                    float X_;
                    float Y_;
                    using (TextWriter tw = new StreamWriter("Slippedwafers.csv", true))
                    {
                        X_ = 0;
                        Y_ = 0;

                        if (resuList[m] != 0)
                        {
                            X_ = AllContourPointfs[resuList[m]].X;
                            Y_ = AllContourPointfs[resuList[m]].Y;

                        }

                        tw.WriteLine($"{fileList[m]};{X_};{Y_}");
                    }

                    #endregion

                    Console.WriteLine($"{fileList[m]}");

                }
            }

            Console.WriteLine(Environment.NewLine);
            Console.WriteLine($"{fileList.Length} images processed. {Environment.NewLine}{resuList.Where((p) => p != 0).Count()} slippedwafers found from {fileList.Length} wafer images.");            

            Console.ReadKey();
        }


        /// <summary>
        /// Defines conditions for the vertical edges of the slipped wafers
        /// </summary>
        /// <param name="edgeVectors">array of edge points</param>
        /// <returns></returns>
        private static int EvalVectors(double[,] edgeVectors)
        {
            int counterHoriz = 0;
            int counterVertical = 0;
            int savedVertical = 0;
            for (int i = 1; i < edgeVectors.Length/4 - 2; i++)
            {                
                if (edgeVectors[i, 0] != edgeVectors[i - 1, 0])
                    counterHoriz = 0;

                if ((edgeVectors[i, 0] == 1 || edgeVectors[i, 0] == -1) && edgeVectors[i, 1] == 0)
                {
                    if (counterHoriz == 0)
                        savedVertical = counterVertical;

                    counterHoriz++;
                }
                else
                    counterHoriz = 0;

                if (edgeVectors[i, 1] != edgeVectors[i - 1, 1])
                    counterVertical = 0;

                if (edgeVectors[i, 1] == 1 || edgeVectors[i, 1] == -1)
                    counterVertical++;

                if (savedVertical >= _verticalThreshold && counterHoriz >= _horizontalThreshold)
                    return i;
            }

            return 0;       // 0 is the "not found", because the result cannot be 0
        }

    }
}
