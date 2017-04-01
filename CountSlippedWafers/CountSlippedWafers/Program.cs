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

        #endregion


        #region read in image:

        inputArray = File.ReadAllBytes(@"f:\_WORK\_SEMILAB\_SW_PROJECTS\CountSlippedWafers_Images\1003_20170318_080045_NOK.raw");


            for (int j = 0; j < height / 2; j++)
            {
                int rowWidth1 = j * 2 * width * 2;
                int rowWidth2 = (j * 2 + 1) * width * 2;

                for (int i = 0; i < width; i++)
                {
                    int columnWidth = 2 * i;

                    array1[j * width + i] = (byte)(inputArray[rowWidth1 + columnWidth +1 ] *16); // + inputArray[rowWidth1 + columnWidth]);                    //its like div16 -> only the higher value bit is kept



                    array2[j * width + i] = (byte)(inputArray[rowWidth2 + columnWidth +1] *16); // + inputArray[rowWidth2 + columnWidth];
                }
            }

            image1.Bytes = array1; 
            image1.Bytes = array2;


            #endregion

            WaferMask = image1.ThresholdBinary(new Gray(50), new Gray(255)).Convert<Gray, byte>();
            //Contour<Point> contours = WaferMask.FindContours

            Image<Gray, Byte> cannyEdges = gray.Canny(cannyThreshold, cannyThresholdLinking);
            for (Contour<Point> contours = cannyEdges.FindContours(); contours != null; contours = contours.HNext)
            {
                ;
            }

                ImageViewer.Show(WaferMask, "Image1");


            Console.ReadKey();
        }
    }
}
