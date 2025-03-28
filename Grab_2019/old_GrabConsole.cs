using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Runtime.InteropServices;

using DALSA.SaperaLT.SapClassBasic;
using DALSA.SaperaLT.Examples.NET.Utils;
using System.Security.Cryptography;
using System.Threading;

namespace DALSA.SaperaLT.Examples.NET.CSharp.GrabConsole
{
    class GrabConsole {
        static float lastFrameRate = 0.0f;
        public static SapAcquisition Acq = null;
        public static SapAcqDevice AcqDevice = null;
        public static SapBuffer Buffers = null;
        public static SapTransfer Xfer = null;
        public static SapView View = null;
        public static SapLocation Loc;
        public static UInt16[,,] framesArr;
        public static byte countFrame = 0;
        public static byte numFrames = 10;
        public const int MAX_TIME = 1024;
        public const ushort BLOCK_SIZE = 16384;

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern bool AllocConsole();

        public static void InitializeFrameArray(byte dim1, UInt16 dim2, UInt16 dim3) {
            Console.WriteLine("[InitFrameArray] dim1 {0} | dim2 {1} | dim3 {2}", dim1, dim2, dim3);
            framesArr = (UInt16[,,])Array.CreateInstance(typeof(UInt16), dim1, dim2, dim3);
        }

        public static void SaveFrameArray(UInt16 width, UInt16 height, IntPtr buffAddress) {
        
            int min = 0, max = 0;
            int[] intArr = new int[BLOCK_SIZE];
            Random rdn = new Random();

            for (byte block = 0; block < height; block++)
            {
                Marshal.Copy(buffAddress + (block * BLOCK_SIZE * sizeof(UInt16)), intArr, 0, BLOCK_SIZE);

                for (int i = 0; i < BLOCK_SIZE; i++)
                {
                    framesArr[countFrame, block, i] = (UInt16)intArr[i];
                    if (framesArr[countFrame, block, i] > max)
                    {
                        max = framesArr[countFrame, block, i];
                    }
                    else if (framesArr[countFrame, block, i] < min)
                    {
                        min = framesArr[countFrame, block, i];
                    }
                }
            }
            countFrame++;
            var n1 = rdn.Next(countFrame);
            var n2 = rdn.Next(128);
            var n3 = rdn.Next(BLOCK_SIZE);
            Console.WriteLine("max: {0} | min: {1} | countFrame: {2} | randValue: {3},{4},{5} = {6}", max, min, countFrame, n1, n2, n3, framesArr[n1, n2, n3]);
        }
        static void xfer_XferNotify(object sender, SapXferNotifyEventArgs args) {
            
            // refresh view
            SapView View = args.Context as SapView;
            View.Show();

            // Save image buffer
            if (Buffers != null && Buffers.GetAddress(out IntPtr buffAddress)) {
                while (countFrame < numFrames) {
                    SaveFrameArray((UInt16) (Buffers.Width), (UInt16) (Buffers.Height), buffAddress);
                }
            }
            else {
                return;
            }

            // refresh frame rate
            SapTransfer transfer = sender as SapTransfer;
            if (transfer.UpdateFrameRateStatistics()) {
                SapXferFrameRateInfo stats = transfer.FrameRateStatistics;
                float framerate = 0.0f;

                if (stats.IsLiveFrameRateAvailable)
                    framerate = stats.LiveFrameRate;

                // check if frame rate is stalled
                if (stats.IsLiveFrameRateStalled) {
                    Console.WriteLine("Live Frame rate is stalled.");
                }

                // update FPS only if the value changed by +/- 0.1
                else if ((framerate > 0.0f) && (Math.Abs(lastFrameRate - framerate) > 0.1f))
                {
                   Console.WriteLine("Grabbing at {0} frames/sec", framerate);
                   lastFrameRate = framerate;
                }
            }
        }

        public static void ProcessFrame(SapView view)
        {
            view.Show();
            if (Buffers != null && Buffers.GetAddress(out IntPtr buffAddress))
            {
                SaveFrameArray((UInt16)(Buffers.Width), (UInt16)(Buffers.Height), buffAddress);

            }
        }

        static void Main(string[] args)
        {
            AllocConsole();
            Console.WriteLine("Sapera Console Grab Example (Polling Mode)");

            MyAcquisitionParams acqParams = new MyAcquisitionParams();

            if (!GetOptions(args, acqParams))
            {
                Console.WriteLine("\nPress any key to terminate\n");
                Console.ReadKey(true);
                return;
            }

            SapLocation loc = new SapLocation(acqParams.ServerName, acqParams.ResourceIndex);

            if (SapManager.GetResourceCount(acqParams.ServerName, SapManager.ResourceType.Acq) > 0)
            {
                Acq = new SapAcquisition(loc, acqParams.ConfigFileName);
                Buffers = new SapBufferWithTrash(2, Acq, SapBuffer.MemoryType.ScatterGather);
                Xfer = new SapAcqToBuf(Acq, Buffers);

                if (!Acq.Create())
                {
                    Console.WriteLine("Error creating SapAcquisition!");
                    DestroysObjects(Acq, AcqDevice, Buffers, Xfer, View);
                    return;
                }
            }

            View = new SapView(Buffers);

            if (!Buffers.Create() || !Xfer.Create() || !View.Create())
            {
                Console.WriteLine("Error during object creation!");
                DestroysObjects(Acq, AcqDevice, Buffers, Xfer, View);
                return;
            }

            InitializeFrameArray(numFrames, (UInt16)Buffers.Height, (UInt16)Buffers.Width);

            Console.WriteLine("Starting frame acquisition... Buffer pixel Depth = {0}", Buffers.PixelDepth);
            Xfer.Snap();
            Xfer.Wait(MAX_TIME);  // Aguarda a captura dos frames

            Console.WriteLine("Processing frames...");
            ProcessCapturedFrames();

            DestroysObjects(Acq, AcqDevice, Buffers, Xfer, View);
            loc.Dispose();
        }

        public static void ProcessCapturedFrames()
        {
            for (int i = 0; i < numFrames; i++)
            {
                if (Buffers.GetAddress(out IntPtr buffAddress))
                {
                    SaveFrameArray((UInt16)(Buffers.Width), (UInt16)(Buffers.Height), buffAddress);
                }
                else
                {
                    Console.WriteLine("Error accessing buffer!");
                }
            }
        }
        public static void Xfer_XferNotify(object sender, SapXferNotifyEventArgs args)
        {
            // Verify if Xfer it is not null and active yet
            if (Xfer == null || !Xfer.Grabbing)
            {
                return;
            }

            SapView view = args.Context as SapView;

            if (view == null)
            {
                return;
            }

            // Update visualization
            view.Show();

            //Save image buffer
            if (Buffers != null && Buffers.GetAddress(out IntPtr buffAddress))
            {
                SaveFrameArray((UInt16)(Buffers.Width), (UInt16)(Buffers.Height), buffAddress);
            }
            else
            {
                return;
            }

        }

        static bool GetOptions(string[] args, MyAcquisitionParams acqParams)
        {
        // Check if arguments were passed
        if (args.Length > 1)
            return ExampleUtils.GetOptionsFromCommandLine(args, acqParams);
        else
            return ExampleUtils.GetOptionsFromQuestions(acqParams);
         }



        static void DestroysObjects(SapAcquisition acq, SapAcqDevice camera, SapBuffer buf, SapTransfer xfer, SapView view)
    {

        if (Xfer != null)
        {
            Xfer.Destroy();
            Xfer.Dispose();
        }

        if (Buffers != null)
        {
            Buffers.Destroy();
            Buffers.Dispose();
        }


        if (AcqDevice != null)
        {
            AcqDevice.Destroy();
            AcqDevice.Dispose();
        }

        if (Acq != null)
        {
            Acq.Destroy();
            Acq.Dispose();
        }


        if (View != null)
        {
            View.Destroy();
            View.Dispose();
        }

        if (countFrame >= numFrames)
        {
            countFrame = 0;
        }

        Console.WriteLine("\nPress any key to terminate\n");
        Console.ReadKey(true);
        }
    }
}
