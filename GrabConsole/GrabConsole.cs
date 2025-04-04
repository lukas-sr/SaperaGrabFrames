using System;
using DALSA.SaperaLT.Examples.NET.Utils;
using GrabFramesGeneral;

namespace DALSA.SaperaLT.Examples.NET.CSharp.GrabConsole
{
    class GrabConsole
    {
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern bool AllocConsole();
        static void Main(string[] args)
        {
            AllocConsole();
            Console.WriteLine("Sapera Console Grab Example (Polling Mode)");

            // Creating acquisiton params to populate with console options
            MyAcquisitionParams acqParams = new MyAcquisitionParams();

            if (!GetOptions(args, acqParams))
            {
                Console.WriteLine("\nPress any key to terminate\n");
                Console.ReadKey(true);
                return;
            }

            // Init class with constructor of the method with acquisiton params populated
            SaperaFrames _saperaFrames = new SaperaFrames(
                acqParams.ServerName,
                acqParams.ConfigFileName
            );

            // Configure transfer objects
            Console.WriteLine("Configuring...");
            _saperaFrames.ConfigureTransfer();

            // Configure transfer events
            _saperaFrames.ConfigureTransferEvents();

            byte nFrames = 10;
            // Create the objects 
            Console.WriteLine("Creting Objects...");
            _saperaFrames.CreateObjects();

            // Start grabbing
            Console.WriteLine("Processing frames...");
            _saperaFrames.StartGrabbing(nFrames);

            ushort[,,] framesTotal = _saperaFrames.ProcessCapturedFrames();

            //print dimensions for framesTotal variable
            Console.WriteLine("Dimension 0: {0}", framesTotal.GetLength(0));
            Console.WriteLine("Dimension 1: {0}", framesTotal.GetLength(1));
            Console.WriteLine("Dimension 2: {0}", framesTotal.GetLength(2));
            Console.WriteLine("RandomValue: {0}", framesTotal[0, 0, 100]);

            _saperaFrames.DestroyAll();
        }
        static bool GetOptions(string[] args, MyAcquisitionParams acqParams)
        {
            // Check if arguments were passed
            if (args.Length > 1)
                return ExampleUtils.GetOptionsFromCommandLine(args, acqParams);
            else
                return ExampleUtils.GetOptionsFromQuestions(acqParams);
        }
    }
}
