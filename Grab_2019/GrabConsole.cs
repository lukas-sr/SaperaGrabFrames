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

            MyAcquisitionParams acqParams = new MyAcquisitionParams();

            if (!GetOptions(args, acqParams))
            {
                Console.WriteLine("\nPress any key to terminate\n");
                Console.ReadKey(true);
                return;
            }

            SaperaFrames _saperaFrames = new SaperaFrames(
                acqParams.ServerName,
                acqParams.ConfigFileName,
                nFrames: 10
            );

            _saperaFrames.ConfigureTransfer();

            Console.WriteLine("Processing frames...");
            while (_saperaFrames.CapturedFrames < _saperaFrames.numFrames)
            {
                _saperaFrames.StartGrabbing();
                Thread.Sleep(100);
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
    }
}
