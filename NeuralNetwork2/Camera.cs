using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Diagnostics;
using AForge.Imaging.Filters;
using AForge.Video;
using AForge.Video.DirectShow;
using Accord.Neuro;

namespace NeuralNetwork2
{
    class MagicEye
    {

        private readonly object balanceLock = new object();
        // обработанное изображение 
        public AForge.Imaging.UnmanagedImage processed;
        // оригинал и финальное
        public Bitmap original, number;
        public int BlobCount { get; private set; }
        public bool Recongnised { get; private set; }
        public float Angle { get; private set; }
        public float AngleRad { get; private set; }
        public float ThresholdValue = 0.15f;

        private static MagicEye empty_proccor = new MagicEye();


        // filters objects
        Grayscale grayFilter;
        ResizeBilinear scaleFilter;
        BradleyLocalThresholding threshldFilter;
        Invert InvertFilter;
        AForge.Imaging.BlobCounter Blober;
        
        Graphics g;
        public MagicEye()
        {
            grayFilter = new AForge.Imaging.Filters.Grayscale(0.2125, 0.7154, 0.0721);
            scaleFilter = new AForge.Imaging.Filters.ResizeBilinear(300, 300);
            threshldFilter = new AForge.Imaging.Filters.BradleyLocalThresholding();
            InvertFilter = new AForge.Imaging.Filters.Invert();
            Blober = new AForge.Imaging.BlobCounter();
            original = new Bitmap(300, 300);
            g = Graphics.FromImage(original);
            Blober.FilterBlobs = true;
            Blober.MinWidth = 5;
            Blober.MinHeight = 5;
            Blober.ObjectsOrder = AForge.Imaging.ObjectsOrder.Size;
        }


        public void ProcessImage(Bitmap input_image)
        {
            lock (balanceLock)
            {
            int side =Math.Min(input_image.Height,input_image.Width);
            Rectangle cropRect = new Rectangle(0, 0 ,side, side); // this is square that represents feed from camera
            //g.DrawImage(input_image, new Rectangle(0, 0, input_image.Width, input_image.Height), cropRect, GraphicsUnit.Pixel); // place it on original bitmap         
                original = new Bitmap(input_image);                                                                                                                // set new processed
            if (processed != null)
                processed.Dispose();  //  Конвертируем изображение в градации серого
            processed = grayFilter.Apply(AForge.Imaging.UnmanagedImage.FromManagedImage(original));
            //  Пороговый фильтр применяем. Величина порога берётся из настроек, и меняется на форме
            threshldFilter.PixelBrightnessDifferenceLimit = ThresholdValue;
            threshldFilter.ApplyInPlace(processed);  
            InvertFilter.ApplyInPlace(processed);
            Blober.ProcessImage(processed);
            AForge.Imaging.Blob[] blobs = Blober.GetObjectsInformation();
            BlobCount = blobs.Length;
            
            if (blobs.Length > 0)
            {
                var BiggestBlob = blobs[0];
                Recongnised = true;
                Blober.ExtractBlobsImage(processed, BiggestBlob, false);
                processed = BiggestBlob.Image;
                AForge.Point mc = BiggestBlob.CenterOfGravity;
                AForge.Point ic = new AForge.Point((float)BiggestBlob.Image.Width / 2, (float)BiggestBlob.Image.Height / 2);
                AngleRad = (ic.Y - mc.Y) / (ic.X - mc.X);
                Angle =  (float)(Math.Atan(AngleRad) * 180 / Math.PI);
               
                 
            }
            else
            {
                // TODO make arrengaments for No blobs case
                Recongnised = false;
                Angle = 0;
                AngleRad = -1;
               
            }

            if (number != null)
                number.Dispose();
            number = processed.ToManagedImage();

            }

        }

     
        public AForge.Imaging.UnmanagedImage GetInput(out double[] input,int countBlocks = 100)
        {         
            double toDbl(Color c)
            {
                var s =c.R + c.B + c.G;
                if (s > 50)
                    return 1.0;
                return -1;

            }

            input = new double[countBlocks * countBlocks + 1];
            AForge.Imaging.UnmanagedImage img;
            var filter = new ResizeBicubic(countBlocks, countBlocks);
            double angle;
            
            lock (balanceLock)
            {
               angle = AngleRad;
                if (processed.Height == countBlocks && processed.Width == countBlocks)
                    img = processed.Clone();
                else
                    img = filter.Apply(processed);
            }
            
            double max_input = 0;
            for (int i = 0; i < countBlocks; i++)
            {
                for (int j = 0; j < countBlocks; j++)
                {
                    var d  = toDbl(img.GetPixel(i, j));
                    input[i * countBlocks + j] = d;
                    if (d > max_input)
                        max_input = d;
                }

            }
            if(max_input !=0)
                for (int i = 0; i < input.Length-1; i++)
                {
                    input[i] /= max_input;
                    
                }
            input[countBlocks * countBlocks] = AngleRad / Math.PI/2.0;
            return img;

        }

     
        public void GetInputLines(out double[] inp, int countBlocks)
        {         
            var input = new double[countBlocks*2+1].Select(d => 0.0).ToArray();
            AForge.Imaging.UnmanagedImage img;
            var filter = new ResizeBicubic(countBlocks, countBlocks);
            double angle;

            lock (balanceLock)
            {
                angle = AngleRad;
                if (processed.Height == countBlocks && processed.Width == countBlocks)
                    img = processed.Clone();
                else
                    img = filter.Apply(processed);
            }

          

            img.CollectActivePixels().ForEach(p => { input[p.Y] += 1; input[countBlocks+p.X] += 1; });
            var mx = input.Max();
            if(mx !=0)
            for (int i = 0; i < input.Length; i++)
            {
                    input[i] /= mx;
            }
            input[input.Length - 1] = (double)angle/Math.PI/2;

            inp = input;

        }

        public static void GetInputFromPath(string path, out double[] input, int countBlocks = 100)
        {
            empty_proccor.processed = AForge.Imaging.UnmanagedImage.FromManagedImage(new Bitmap(path));
            empty_proccor.GetInput(out input, countBlocks);
        }

        public void GetPicture(out Bitmap or, out Bitmap num)
        {
            lock (balanceLock)
            {
                or = new Bitmap(original);
                num = new Bitmap(number);

            }
        }

        private AForge.Imaging.UnmanagedImage sav;
        public void presave()
        {
            lock (balanceLock)
            {
                sav = AForge.Imaging.UnmanagedImage.FromManagedImage(number);

            }

        }

        public AForge.Imaging.UnmanagedImage GetImage()
        {
            ResizeBicubic resize = new ResizeBicubic(28, 28);
            lock (balanceLock)
            {
                return resize.Apply(processed);
            }
            
        }


        public void Save(string path)
        {
            ResizeBicubic resize = new ResizeBicubic(28, 28);
            resize.Apply(sav).ToManagedImage().Save(path);
        }

    }
}

