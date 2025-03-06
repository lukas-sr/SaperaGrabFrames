using System;
using System.Runtime.InteropServices;
using DALSA.SaperaLT.SapClassBasic;

namespace GrabFramesGeneral
{
    public class SaperaFrames
    {
        private SapAcquisition _acquisition;
        private SapAcqDevice _acqDevice;
        private SapBuffer _buffers;
        private SapTransfer _transfer;
        private SapView _view;
        private SapLocation _location;  
        public static MyAcquisitionParams _acqParams;
        public ushort[,,] framesArray { get; private set; }
        public byte numFrames { get; }
        private byte _countFrame;
        private readonly AcquisitionMode _mode;
        private const int MaxTime = 255;
        public ushort blockSize { get; private set; }
        private enum CameraModel
        {
            XtiumCLHSPx8_1,
            Xtium2CLHSPx8_1
        }
        
        public SaperaFrames(string serverName, byte nFrames) {
            if (string.IsNullOrEmpty(serverName))
                throw new ArgumentException("Server name cannot be null or empty", nameof(serverName));

            _acqParams = new MyAcquisitionParams {
                ResourceIndex = 0,
                ServerName = serverName
            };

            numFrames = nFrames;
            InitializeCameraResources(serverName);
        }

        private void InitializeCameraResources(string serverName) {
            _location = new SapLocation(serverName, 0);
            
            // Configuração inicial baseada no modelo da câmera
            var model = IdentifyCameraModel(serverName);
            blockSize = model switch
            {
                CameraModel.XtiumCLHSPx8_1 => 12288,
                CameraModel.Xtium2CLHSPx8_1 => 16384,
                _ => throw new NotSupportedException($"Unsupported camera model: {serverName}")
            };
        }

        private CameraModel IdentifyCameraModel(string serverName)
        {
            return serverName switch
            {
                "Xtium-CLHS_PX8_1" => CameraModel.XtiumCLHSPx8_1,
                "Xtium2-CLHS_PX8_1" => CameraModel.Xtium2CLHSPx8_1,
                _ => throw new ArgumentException($"Unsupported camera: {serverName}")
            };
        }

        // public bool SetConfig(string filePath, string mode) {
        //     acqParams.ConfigFileName = filePath;

        //     if (acqParams.ConfigFileName == null) {
        //         return false;
        //     }
            
        //     if (mode == "AREA") {
        //         modeFlag = 1;
        //     }

        //     if (acqParams.ServerName.Equals(camsAvailable[0])) {
        //         BLOCK_SIZE = 12288;
        //         return true;
        //     }
        //     else if (acqParams.ServerName.Equals(camsAvailable[1])) {
        //         BLOCK_SIZE = 16384;
        //         return true;
        //     }

        //     return false;
        // }
        public void InitializeFrameArray(byte dim1, int dim2, int dim3)
        {
            if (dim1 == 0 || dim2 <= 0 || dim3 <= 0)
                throw new ArgumentException("Invalid array dimensions");

            FramesArray = new ushort[dim1, dim2, dim3];
        }

        // public unsafe void ProcessFrameBuffer(IntPtr bufferAddress, uint bufferSize)
        // {
        //     if (bufferAddress == IntPtr.Zero)
        //         throw new ArgumentNullException(nameof(bufferAddress));

        //     var numBlocks = (int)Math.Ceiling((double)bufferSize / BlockSize);
        //     var blockSizeBytes = blockSize * sizeof(short);

        //     for (var block = 0; block < numBlocks; block++)
        //     {
        //         var sourceAddress = bufferAddress + block * blockSizeBytes;
        //         var buffer = new short[BlockSize];
                
        //         Marshal.Copy(sourceAddress, buffer, 0, blockSize);

        //         for (var i = 0; i < BlockSize; i++)
        //         {
        //             // 8. Conversão segura usando unchecked
        //             FramesArray[_countFrame, block, i] = unchecked((ushort)buffer[i]);
        //         }
        //     }

        //     _countFrame++;
        // }


        // public bool ConfigGrab() {
        //     loc = new SapLocation(acqParams.ServerName, acqParams.ResourceIndex);

        //     if (SapManager.GetResourceCount(acqParams.ServerName, SapManager.ResourceType.Acq) > 0) {
        //         Acq = new SapAcquisition(loc, acqParams.ConfigFileName);
        //         Buffers = new SapBufferWithTrash(2, Acq, SapBuffer.MemoryType.ScatterGather);
        //         Xfer = new SapAcqToBuf(Acq, Buffers);

        //         // Create acquisition object
        //         if (!Acq.Create()) {
        //             DestroysObjects();
        //             return false;
        //         }

        //         if (Acq != null && Acq.IsCapabilityAvailable(SapAcquisition.Cap.EVENT_TYPE)) {
        //             Acq.EnableEvent(SapAcquisition.AcqEventType.StartOfFrame);
        //             return true;
        //         }
        //         else {
        //             DestroysObjects();
        //             return false;
        //         }
        //     }

        //     else if (SapManager.GetResourceCount(acqParams.ServerName, SapManager.ResourceType.AcqDevice) > 0) {
        //         AcqDevice = new SapAcqDevice(loc, acqParams.ConfigFileName);
        //         Buffers = new SapBufferWithTrash(2, AcqDevice, SapBuffer.MemoryType.ScatterGather);
        //         Xfer = new SapAcqDeviceToBuf(AcqDevice, Buffers);

        //         // Create acquisition object
        //         if (!AcqDevice.Create()) {
        //             DestroysObjects();
        //             return false;
        //         }
        //     }

        //     View = new SapView(Buffers);
        //     // End of frame event
        //     Xfer.Pairs[0].EventType = SapXferPair.XferEventType.EndOfFrame;
        //     Xfer.XferNotify += new SapXferNotifyHandler(Xfer_XferNotify);
        //     Xfer.XferNotifyContext = View;

        //     return true;
        // }

        // public virtual void Xfer_XferNotify(object sender, SapXferNotifyEventArgs args) {
        //     // Verify if Xfer it is not null and active yet
        //     if (Xfer == null || !Xfer.Grabbing) {
        //         return;
        //     }

        //     SapView view = args.Context as SapView;
        //     if (view == null) {
        //         return;
        //     }

        //     // Update visualization
        //     view.Show();

        //     // Save image buffer
        //     if (Buffers != null && Buffers.GetAddress(out IntPtr buffAddress)) {
        //         SaveFrameArray((UInt32)(Buffers.Width), buffAddress);
        //     }
        //     else {
        //         return;
        //     }
        // }

        // public void ProcessFrame()
        // {
        //     // Simulate processing and saving the frame
        //     if (Buffers != null && Buffers.GetAddress(out IntPtr buffAddress)) {
        //         SaveFrameArray((UInt32)(Buffers.Width), buffAddress);
        //     }
        // }

        // public static void DestroysObjects() {
        //     if (Xfer != null) {
        //         Xfer.Destroy();
        //         Xfer.Dispose();
        //     }

        //     if (Buffers != null) {
        //         Buffers.Destroy();
        //         Buffers.Dispose();
        //     }


        //     if (AcqDevice != null) {
        //         AcqDevice.Destroy();
        //         AcqDevice.Dispose();
        //     }

        //     if (Acq != null) {
        //         Acq.Destroy();
        //         Acq.Dispose();
        //     }


        //     if (View != null) {
        //         View.Destroy();
        //         View.Dispose();
        //     }

        //     if (countFrame >= numFrames) {
        //         countFrame = 0;
        //     }
        // }
        public void ConfigureTransfer()
        {
            try
            {
                if (SapManager.GetResourceCount(_location.ServerName, SapManager.ResourceType.Acq) > 0)
                {
                    _acquisition = new SapAcquisition(_location, null);
                    ConfigureResources(_acquisition);
                }
                else if (SapManager.GetResourceCount(_location.ServerName, SapManager.ResourceType.AcqDevice) > 0)
                {
                    _acqDevice = new SapAcqDevice(_location, null);
                    ConfigureResources(_acqDevice);
                }

                _view = new SapView(_buffers);
                ConfigureTransferEvents();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Configuration failed", ex);
            }
        }

        private void ConfigureResources(dynamic acquisitionObject)
        {
            _buffers = new SapBufferWithTrash(2, acquisitionObject, SapBuffer.MemoryType.ScatterGather);
            _transfer = new SapAcqToBuf(acquisitionObject, _buffers);

            if (!acquisitionObject.Create())
            {
                Dispose();
                throw new InvalidOperationException("Failed to create acquisition object");
            }
        }

        private void ConfigureTransferEvents()
        {
            _transfer.Pairs[0].EventType = SapXferPair.XferEventType.EndOfFrame;
            _transfer.XferNotify += HandleTransferNotification;
            _transfer.XferNotifyContext = _view;
        }

        private void HandleTransferNotification(object sender, SapXferNotifyEventArgs e)
        {
            if (_transfer == null || !_transfer.Grabbing) return;

            if (e.Context is SapView view && _buffers != null)
            {
                try
                {
                    view.Show();
                    if (_buffers.GetAddress(out IntPtr address))
                    {
                        ProcessFrameBuffer(address, (uint)_buffers.Width);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing frame: {ex.Message}");
                }
            }
        }

        public bool IsGrabbing => _transfer?.Grabbing ?? false;
        public int CapturedFrames => _countFramez
    }
}
