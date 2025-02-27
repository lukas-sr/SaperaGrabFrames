using System;
using System.Runtime.InteropServices;
using DALSA.SaperaLT.SapClassBasic;

public class FramesTDI
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
    private readonly byte MAX_TIME = 255;
    public static String[] camsAvailable = {"Xtium-CLHS_PX8_1", "Xtium2-CLHS_PX8_1" };

    public FramesTDI(string serverName)
    {
        acqParams = new MyAcquisitionParams
        {
            ResourceIndex = 0,
            ServerName = serverName
        };
    }
    public static void InitializeFrameArray(byte dim1, UInt32 dim2, UInt32 dim3)
    {
        framesArr = (UInt16[,,])Array.CreateInstance(typeof(UInt16), dim1, dim2, dim3);
    }
    public static void SaveFrameArray(int size, IntPtr buffAddress)
    {
        int[] intArr = new int[size];

        Marshal.Copy(buffAddress, intArr, 0, size);

        for (int i = 0; i < size; i++)
        {
            framesArr[countFrame, 0, i] = (UInt16)intArr[i];
        }
        countFrame++;
    }
    public bool setConfigFile(string filePath)
    {
        acqParams.ConfigFileName = filePath;

        if ((acqParams.ConfigFileName != null) && ((acqParams.ServerName.Equals(camsAvailable[0])) || (acqParams.ServerName.Equals(camsAvailable[1])) ) )
        {
            return true;
        }

        return false;
    }
    public bool ConfigureGrabTDI()
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
    public void StartSnap()
    {
        // Create buffer object
        if (!Buffers.Create())
        {
            DestroysObjects();
            return;
        }

        // For TDI Case the heigth of the buffer is equal to 1
        InitializeFrameArray(numFrames, 1, (UInt32)Buffers.Width);

        // Create buffer object
        if (!Xfer.Create())
        {
            DestroysObjects();
            return;
        }

        // Create buffer object
        if (!View.Create())
        {
            DestroysObjects();
            return;
        }
        Xfer.Snap((int)numFrames);

        Xfer.Wait(numFrames * MAX_TIME);
        DestroysObjects();
        loc.Dispose();
    }
    public virtual void Xfer_XferNotify(object sender, SapXferNotifyEventArgs args)
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

        // Save image buffer
        if (Buffers != null && Buffers.GetAddress(out IntPtr buffAddress))
        {
            while(countFrame < numFrames)
            {
                SaveFrameArray((Buffers.Width), buffAddress);
            }
        }
        else
        {
            return;
        }
    
    }
    
    public static void ReInitializeCountFrame()
    {
        countFrame = 0;
    }
    
    public static void DestroysObjects()
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
    }
}
