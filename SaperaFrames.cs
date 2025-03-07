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
        private bool _disposed;
        public static MyAcquisitionParams _acqParams;
        public ushort[,,] framesArray { get; private set; }
        public byte numFrames { get; }
        private byte _countFrame;
        private const int MaxTime = 255;
        public ushort blockSize { get; private set; }
        private enum CameraModel
        {
            XtiumCLHSPx8_1,
            Xtium2CLHSPx8_1
        }
        
        public SaperaFrames(string serverName, string filePath, byte nFrames) {
            if (string.IsNullOrEmpty(serverName))
                throw new ArgumentException("Server name cannot be null or empty", nameof(serverName));

            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

            _acqParams = new MyAcquisitionParams {
                ResourceIndex = 0,
                ServerName = serverName,
                ConfigFileName = filePath
            };

            numFrames = nFrames;
            InitializeCameraResources(serverName);
        }

        private void InitializeCameraResources(string serverName) {
            _location = new SapLocation(serverName, 0);
            
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
        public void InitializeFrameArray(byte dim1, int dim2, int dim3)
        {
            if (dim1 == 0 || dim2 <= 0 || dim3 <= 0)
                throw new ArgumentException("Invalid array dimensions");

            framesArray = new ushort[dim1, dim2, dim3];
        }

        public unsafe void ProcessFrameBuffer(IntPtr bufferAddress, uint bufferSize)
        {
            if (bufferAddress == IntPtr.Zero)
                throw new ArgumentNullException(nameof(bufferAddress));

            var numBlocks = (int)Math.Ceiling((double)bufferSize / blockSize);
            var blockSizeBytes = blockSize * sizeof(short);

            for (var block = 0; block < numBlocks; block++)
            {
                var sourceAddress = bufferAddress + block * blockSizeBytes;
                var buffer = new short[blockSize];
                
                Marshal.Copy(sourceAddress, buffer, 0, blockSize);

                for (var i = 0; i < blockSize; i++)
                {
                    framesArray[_countFrame, block, i] = unchecked((ushort)buffer[i]);
                }
            }

            _countFrame++;
        }

        public void ConfigureTransfer() {
            try {
                if (SapManager.GetResourceCount(_location.ServerName, SapManager.ResourceType.Acq) > 0) {
                    _acquisition = new SapAcquisition(_location, _acqParams.ConfigFileName);
                    ConfigureResources(_acquisition);
                }
                else if (SapManager.GetResourceCount(_location.ServerName, SapManager.ResourceType.AcqDevice) > 0) {
                    _acqDevice = new SapAcqDevice(_location, _acqParams.ConfigFileName);
                    ConfigureResources(_acqDevice);
                }

                _view = new SapView(_buffers);
                ConfigureTransferEvents();
            }
            catch (Exception ex) {
                throw new InvalidOperationException("Configuration failed", ex);
            }
        }

        private void ConfigureResources(dynamic acquisitionObject) {
            _buffers = new SapBufferWithTrash(2, acquisitionObject, SapBuffer.MemoryType.ScatterGather);
            _transfer = new SapAcqToBuf(acquisitionObject, _buffers);

            if (!acquisitionObject.Create())
            {
                Dispose();
                throw new InvalidOperationException("Failed to create acquisition object");
            }
        }

        private void ConfigureTransferEvents() {
            _transfer.Pairs[0].EventType = SapXferPair.XferEventType.EndOfFrame;
            _transfer.XferNotify += HandleTransferNotification;
            _transfer.XferNotifyContext = _view;
        }

        private void HandleTransferNotification(object sender, SapXferNotifyEventArgs e)
        {
            if (_transfer == null || !_transfer.Grabbing) return;

            if (e.Context is SapView view && _buffers != null) {
                try {
                    view.Show();
                    if (_buffers.GetAddress(out IntPtr address)) {
                        ProcessFrameBuffer(address, (uint)_buffers.Width);
                    }
                }
                catch (Exception ex) {
                    Console.WriteLine($"Error processing frame: {ex.Message}");
                }
            }
        }
        public void StartGrabbing()
        {
            if (_transfer == null) return;
            
            _transfer.Snap();
            _transfer.Wait(MaxTime);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                _transfer?.Dispose();
                _buffers?.Dispose();
                _acqDevice?.Dispose();
                _acquisition?.Dispose();
                _view?.Dispose();
            }

            _disposed = true;
        }

        public bool IsGrabbing => _transfer?.Grabbing ?? false;
        public int CapturedFrames => _countFrame;
    }
}
