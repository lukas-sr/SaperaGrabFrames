using System;
using System.Runtime.InteropServices;
using DALSA.SaperaLT.SapClassBasic;

namespace GrabFramesGeneral
{
    public class SaperaFrames
    {
        public static SapAcquisition Acq = null;
        public static SapAcqDevice AcqDevice = null;
        public static SapBuffer Buffers = null;
        public static SapTransfer Xfer = null;
        public static SapView View = null;
        public static SapLocation loc = null;
        public static MyAcquisitionParams acqParams;
        public static UInt16[,,] framesArr;
        public static byte numFrames = 1;
        public static byte countFrame = 0;
        public static byte modeFlag = 0; // 0 - TDI, 1 - AREA
        public const byte MAX_TIME = 255;
        public static UInt16 BLOCK_SIZE;
        public readonly String[] camsAvailable = { "Xtium-CLHS_PX8_1", "Xtium2-CLHS_PX8_1" };

        public SaperaFrames(string serverName) {
            acqParams = new MyAcquisitionParams
            {
                ResourceIndex = 0,
                ServerName = serverName
            };
        }
        public bool SetConfig(string filePath, string mode) {
            acqParams.ConfigFileName = filePath;

            if (acqParams.ConfigFileName == null) {
                return false;
            }
            
            if (mode == "AREA") {
                modeFlag = 1;
            }

            if (acqParams.ServerName.Equals(camsAvailable[0])) {
                BLOCK_SIZE = 12288;
                return true;
            }
            else if (acqParams.ServerName.Equals(camsAvailable[1])) {
                BLOCK_SIZE = 16384;
                return true;
            }

            return false;
        }
        public static void InitializeFrameArray(byte dim1, UInt32 dim2, UInt32 dim3)
        {
            framesArr = (UInt16[,,])Array.CreateInstance(typeof(UInt16), dim1, dim2, dim3);
        }

        public static void SaveFrameArray(UInt32 size, IntPtr buffAddress)
        {
            int[] intArr = new int[BLOCK_SIZE];
            byte numBlocks = (byte)Math.Ceiling((double)size / BLOCK_SIZE);

            for (byte block = 0; block < numBlocks; block++)
            {
                Marshal.Copy(buffAddress + (block * BLOCK_SIZE * sizeof(Int16)), intArr, 0, BLOCK_SIZE);

                for (int i = 0; i < BLOCK_SIZE; i++)
                {
                    framesArr[countFrame, block, i] = (UInt16)intArr[i];
                }
            }
            countFrame++;
        }

        public bool ConfigGrab()
        {
            loc = new SapLocation(acqParams.ServerName, acqParams.ResourceIndex);

            if (SapManager.GetResourceCount(acqParams.ServerName, SapManager.ResourceType.Acq) > 0)
            {
                Acq = new SapAcquisition(loc, acqParams.ConfigFileName);
                Buffers = new SapBufferWithTrash(2, Acq, SapBuffer.MemoryType.ScatterGather);
                Xfer = new SapAcqToBuf(Acq, Buffers);

                // Create acquisition object
                if (!Acq.Create())
                {
                    DestroysObjects();
                    return false;
                }

                if (Acq != null && Acq.IsCapabilityAvailable(SapAcquisition.Cap.EVENT_TYPE))
                {
                    Acq.EnableEvent(SapAcquisition.AcqEventType.StartOfFrame);
                    return true;
                }
                else
                {
                    DestroysObjects();
                    return false;
                }
            }

            else if (SapManager.GetResourceCount(acqParams.ServerName, SapManager.ResourceType.AcqDevice) > 0)
            {
                AcqDevice = new SapAcqDevice(loc, acqParams.ConfigFileName);
                Buffers = new SapBufferWithTrash(2, AcqDevice, SapBuffer.MemoryType.ScatterGather);
                Xfer = new SapAcqDeviceToBuf(AcqDevice, Buffers);

                // Create acquisition object
                if (!AcqDevice.Create())
                {
                    DestroysObjects();
                    return false;
                }
            }

            View = new SapView(Buffers);
            // End of frame event
            Xfer.Pairs[0].EventType = SapXferPair.XferEventType.EndOfFrame;
            Xfer.XferNotify += new SapXferNotifyHandler(Xfer_XferNotify);
            Xfer.XferNotifyContext = View;

            return true;
        }

        public virtual void Xfer_XferNotify(object sender, SapXferNotifyEventArgs args)
        {
            // Verify if Xfer it is not null and active yet
            if (Xfer == null || !Xfer.Grabbing) {
                return;
            }

            SapView view = args.Context as SapView;
            if (view == null) {
                return;
            }

            // Update visualization
            view.Show();

            // Save image buffer
            if (Buffers != null && Buffers.GetAddress(out IntPtr buffAddress)) {
                while (countFrame < numFrames) {
                    SaveFrameArray((UInt32)(Buffers.Width), buffAddress);
                }
            }
            else {
                return;
            }

        }

        public void ProcessFrame()
        {
            // Simulate processing and saving the frame
            if (Buffers != null && Buffers.GetAddress(out IntPtr buffAddress))
            {
                // Chama explicitamente a função de salvar o quadro
                SaveFrameArray((UInt32)(Buffers.Width), buffAddress);
            }
        }

        public static void DestroysObjects() {
            if (Xfer != null) {
                Xfer.Destroy();
                Xfer.Dispose();
            }

            if (Buffers != null) {
                Buffers.Destroy();
                Buffers.Dispose();
            }


            if (AcqDevice != null) {
                AcqDevice.Destroy();
                AcqDevice.Dispose();
            }

            if (Acq != null) {
                Acq.Destroy();
                Acq.Dispose();
            }


            if (View != null) {
                View.Destroy();
                View.Dispose();
            }

            if (countFrame >= numFrames) {
                countFrame = 0;
            }
        }
    }
}
