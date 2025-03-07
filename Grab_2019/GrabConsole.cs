using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Runtime.InteropServices;
using DALSA.SaperaLT.SapClassBasic;
using DALSA.SaperaLT.Examples.NET.Utils;
using System.Security.Cryptography;
using System.Threading;
using GrabFramesGeneral;

namespace DALSA.SaperaLT.Examples.NET.CSharp.GrabConsole{
    class GrabConsole {
        public static SaperaFrames _saperaFrames;
        private static float lastFrameRate = 0.0f;

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern bool AllocConsole();
        static void xfer_XferNotify(object sender, SapXferNotifyEventArgs args)
        {
            if (_saperaFrames == null || !_saperaFrames.IsGrabbing) return;

            if (args.Context is SapView view)
            {
                _saperaFrames.ProcessFrameBuffer(view);
            }

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

            try
            {
                _saperaFrames = new SaperaFrames(
                    acqParams.ServerName,
                    acqParams.ConfigFileName,
                    numFrames: 10
                );

                _saperaFrames.InitializeFrameArray(
                    dim1: 10,
                    dim2: (int)Buffers.Height,
                    dim3: (int)Buffers.Width
                );

                _saperaFrames.ConfigureTransfer();

                Console.WriteLine("Starting frame acquisition...");
                _saperaFrames.StartGrabbing();

                Console.WriteLine("Processing frames...");
                while (_saperaFrames.CapturedFrames < _saperaFrames.numFrames)
                {
                    Thread.Sleep(100);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
            finally
            {
                _saperaFrames?.Dispose();
                Console.WriteLine("\nPress any key to terminate\n");
                Console.ReadKey(true);
            }
        }

        
    }
}
