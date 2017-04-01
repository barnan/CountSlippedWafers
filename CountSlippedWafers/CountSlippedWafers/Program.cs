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
            #region allocate arrays:

            int width = 4096;
            int height = 8192;
            byte[] inputArray;
            byte[] array1 = new byte[height * width /2];
            byte[] array2 = new byte[height * width /2];

            Image<Gray, Byte> image1 = new Image<Gray, Byte>(width, height/2);
            Image<Gray, Byte> image2 = new Image<Gray, Byte>(width, height/2);

            Image<Gray, Byte> WaferMask;

            PointF[] pointfs;
            PointF[] Allpointfs = null;

            double[,] edgeVectors;

            

            #endregion

            using (storage = new MemStorage())
            {


                #region read in image:

                inputArray = File.ReadAllBytes(@"f:\_WORK\_SEMILAB\_SW_PROJECTS\CountSlippedWafers_Images\1007_20170318_080046_NOK.raw");


                for (int j = 0; j < height / 2; j++)
                {
                    int rowWidth1 = j * 2 * width * 2;
                    int rowWidth2 = (j * 2 + 1) * width * 2;

                    for (int i = 0; i < width; i++)
                    {
                        int columnWidth = 2 * i;

                        array1[j * width + i] = (byte)(inputArray[rowWidth1 + columnWidth + 1] * 16); // + inputArray[rowWidth1 + columnWidth]);                    //its like div16 -> only the higher value bit is kept
                        array2[j * width + i] = (byte)(inputArray[rowWidth2 + columnWidth + 1] * 16); // + inputArray[rowWidth2 + columnWidth];
                    }
                }

                image1.Bytes = array1;
                image1.Bytes = array2;


                #endregion

                //int counter = 0;

                WaferMask = image1.ThresholdBinary(new Gray(50), new Gray(255)).Convert<Gray, byte>();

                //double cannyThresholdLinking = 150.0;
                //Image<Gray, Byte> cannyEdges = image1.Canny(200f, cannyThresholdLinking);

                for (Contour<Point> contours = WaferMask.FindContours(Emgu.CV.CvEnum.CHAIN_APPROX_METHOD.CV_CHAIN_APPROX_NONE, Emgu.CV.CvEnum.RETR_TYPE.CV_RETR_CCOMP, storage); contours != null; contours = contours.HNext)
                {
                    Contour<Point> currentContour = new Contour<Point>(storage);

                    currentContour = contours;
                    image1.DrawPolyline(currentContour.ToArray(), true, new Gray(192), 3);

                    pointfs = Array.ConvertAll(contours.ToArray(), input => new PointF(input.X, input.Y));

                    if (Allpointfs == null || pointfs.Length > Allpointfs.Length)
                        Allpointfs = pointfs;

                    //string filename = "out_" + counter;
                    //using (TextWriter tw = new StreamWriter(filename))
                    //{
                    //    for (int l = 0; l < pointfs.Length; l++)
                    //    {
                    //        tw.WriteLine($"{l};{pointfs[l].X};{pointfs[l].Y}");
                    //    }

                    //}
                    //counter++;
                }

                edgeVectors = new double[Allpointfs.Length - 1, 4];

                for (int i = 1; i < Allpointfs.Length - 2; i++)
                {
                    edgeVectors[i, 0] = Allpointfs[i + 1].X - Allpointfs[i].X;
                    edgeVectors[i, 1] = Allpointfs[i + 1].Y - Allpointfs[i].Y;
                    edgeVectors[i, 2] = Math.Atan2(edgeVectors[i, 1], edgeVectors[i, 0]) * 180 / Math.PI;
                    edgeVectors[i, 3] = edgeVectors[i, 2] + edgeVectors[i-1, 2];
                }

                //using (TextWriter tw = new StreamWriter("edgeVectors.txt"))
                //{
                //    for (int i = 0; i < Allpointfs.Length - 2; i++)
                //        tw.WriteLine($"{edgeVectors[i, 0]} ; {edgeVectors[i, 1]} ; {edgeVectors[i, 2]} ; {edgeVectors[i, 3]}");
                //}

                bool resu = EvalVEctors(edgeVectors);


                ImageViewer.Show(image1, "Image1");

            }

            Console.ReadKey();
        }


        private static bool EvalVEctors(double[,] edgeVectors)
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
                    return true;

            }

            return false;
        }

    }
}
