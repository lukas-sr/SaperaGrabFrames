using System;
using System.IO;
using System.Collections;
using System.Runtime.InteropServices;
using DALSA.SaperaLT.SapClassBasic;
using System.Threading;
using System.Runtime.InteropServices.ComTypes;

namespace GrabFramesGeneral
{
    public class SaperaFrames
    {
        public SapAcquisition _acq = null;
        public SapAcqDevice _acqDevice = null;
        public SapBuffer _buffers = null;
        public SapTransfer _transfer = null;
        public SapView _view = null;
        public SapLocation _location;
        public bool _disposed;
        public MyAcquisitionParams _acqParams;
        public ushort[,,] framesArray { get; private set; }
        public byte numFrames;
        public bool isTDI = false;
        public byte _countFrame;
        private const int MaxTime = 255;
        public ushort blockSize { get; private set; }
        private ushort TDIHeigth = 1;
        private enum CameraModel
        {
            XtiumCLHSPx8_1,
            Xtium2CLHSPx8_1
        }
        public SaperaFrames(string serverName, string filePath)
        {
            if (string.IsNullOrEmpty(serverName))
                throw new ArgumentException("Server name cannot be null or empty", nameof(serverName));

            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

            _acqParams = new MyAcquisitionParams
            {
                ResourceIndex = 0,
                ServerName = serverName,
                ConfigFileName = filePath
            };

            string fileName = Path.GetFileName(filePath);
            isTDI = (fileName.Equals("TDI.ccf", StringComparison.OrdinalIgnoreCase)) ? true : false;

            InitializeCameraResources(serverName);

        }

        private void InitializeCameraResources(string serverName)
        {
            Console.WriteLine("InitializeCameraResources");

            var model = IdentifyCameraModel(serverName);

            if (model == CameraModel.XtiumCLHSPx8_1)
            {
                blockSize = 12288;
            }
            else if (model == CameraModel.Xtium2CLHSPx8_1)
            {
                blockSize = 16384;
            }
            else
            {
                throw new NotSupportedException($"Unsupported camera model: {serverName}");
            }
        }

        private CameraModel IdentifyCameraModel(string serverName)
        {
            CameraModel cameraModel;
            if (serverName == "Xtium-CLHS_PX8_1")
            {
                cameraModel = CameraModel.XtiumCLHSPx8_1;
            }
            else if (serverName == "Xtium2-CLHS_PX8_1")
            {
                cameraModel = CameraModel.Xtium2CLHSPx8_1;
            }
            else
            {
                throw new ArgumentException($"Unsupported camera: {serverName}");
            }

            return cameraModel;
        }
        public void InitializeFrameArray(byte dim1, int dim2, int dim3)
        {
            if (isTDI) dim2 = 1;

            if (dim1 == 0 || dim2 <= 0 || dim3 <= 0) throw new ArgumentException("Invalid array dimensions");

            framesArray = new ushort[dim1, dim2, dim3];
        }

        private unsafe void ProcessFrameBuffer(IntPtr bufferAddress, uint bufferSize)
        {
            if (bufferAddress == IntPtr.Zero)
                throw new ArgumentNullException(nameof(bufferAddress));

            var numBlocks = (int)Math.Ceiling((double)bufferSize / blockSize);
            var blockSizeBytes = blockSize * sizeof(short);

            for (var block = 0; block < numBlocks; block++)
            {
                var sourceAddress = bufferAddress + (block * blockSizeBytes * sizeof(ushort));
                var buffer = new short[blockSize];

                Marshal.Copy(sourceAddress, buffer, 0, blockSize);

                for (var i = 0; i < blockSize; i++)
                {
                    framesArray[_countFrame, block, i] = unchecked((ushort)buffer[i]);
                }
            }

            _countFrame++;
        }

        public void ConfigureTransfer()
        {
            try
            {
                _location = new SapLocation(_acqParams.ServerName, _acqParams.ResourceIndex);

                if (SapManager.GetResourceCount(_location.ServerName, SapManager.ResourceType.Acq) > 0)
                {
                    _acq = new SapAcquisition(_location, _acqParams.ConfigFileName);
                    _buffers = new SapBufferWithTrash(2, _acq, SapBuffer.MemoryType.ScatterGather);
                    _transfer = new SapAcqToBuf(_acq, _buffers);
                }
                else if (SapManager.GetResourceCount(_location.ServerName, SapManager.ResourceType.AcqDevice) > 0)
                {
                    _acqDevice = new SapAcqDevice(_location, _acqParams.ConfigFileName);
                    _buffers = new SapBufferWithTrash(2, _acqDevice, SapBuffer.MemoryType.ScatterGather);
                    _transfer = new SapAcqDeviceToBuf(_acqDevice, _buffers);
                }

                _view = new SapView(_buffers);

            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Configuration failed", ex);
            }
        }
        public void ConfigureTransferEvents()
        {
            _transfer.Pairs[0].EventType = SapXferPair.XferEventType.EndOfFrame;
            _transfer.XferNotify += new SapXferNotifyHandler(TransferCallback); //HandleTransferNotification;
            object[] context = new object[2];
            context[0] = _view;
            context[1] = _buffers;
            _transfer.XferNotifyContext = context;
        }

        public void TransferCallback(object sender, SapXferNotifyEventArgs e)
        {
            if (_transfer == null || _buffers == null || !_transfer.Grabbing) return;

            object[] contextContent = e.Context as object[];
            SapView view = contextContent[0] as SapView;
            SapBuffer pBufferAcq = contextContent[1] as SapBuffer;
            Console.WriteLine(pBufferAcq.Height.ToString());
            try
            {
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
        public void CreateObjects()
        {
            if (!_acq.Create() || !_buffers.Create() || !_transfer.Create())
            {
                Console.WriteLine("Error during object creation");
                DestroyAll();
                return;
            }
        }

        public bool StartGrabbing(byte nFrames)
        {
            _countFrame = 0;
            numFrames = nFrames;
            InitializeFrameArray(numFrames, _buffers.Height, _buffers.Width);
            return _transfer?.Snap(numFrames) ?? false;
        }

        public ushort[,,] ProcessCapturedFrames()
        {
            for (byte i = 0; i < numFrames; i++)
            {
                if (_buffers.GetAddress(out IntPtr buffAddress))
                {
                    SaveFrameArray(
                        (ushort)(_buffers.Width),
                        (isTDI ? TDIHeigth : (ushort)(_buffers.Height)),
                        buffAddress
                    );
                }
                else framesArray.SetValue(-1, 0);
            }
            return framesArray;
        }

        public void DestroyAll()
        {
            if (_transfer != null) { _transfer.Destroy(); _transfer.Dispose(); }

            if (_buffers != null) { _buffers.Destroy(); _buffers.Dispose(); }

            if (_acqDevice != null) { _acqDevice.Destroy(); _acqDevice.Dispose(); }

            if (_acq != null) { _acq.Destroy(); _acq.Dispose(); }

            if (_view != null) { _view.Destroy(); _view.Dispose(); }
        }

        public unsafe void SaveFrameArray(ushort width, ushort height, IntPtr buffAddress)
        {
            byte[] ushortArr = new byte[blockSize];

            if (isTDI) height = 1;

            for (byte block = 0; block < height; block++)
            {
                if (!_buffers.GetParameter(SapBuffer.Prm.PIXEL_DEPTH, out int bitsPixel))
                {
                    return;
                }

                if (bitsPixel >= 12)
                {
                    ushort* dataPtr = (ushort*)(buffAddress + (block * blockSize * sizeof(ushort)));

                    for (int i = 0; i < blockSize; i++)
                    {
                        byte byte1 = (byte)((*(dataPtr + i)) >> 8);
                        byte byte2 = (byte)((*(dataPtr + i)) & 0xFF);

                        framesArray[_countFrame, block, i] = (ushort)(byte1 << 8 | byte2);
                    }
                }
                else
                {
                    byte* dataPtr = (byte*)(buffAddress + (block * blockSize * sizeof(byte)));

                    for (int i = 0; i < blockSize; i++)
                    {
                        ushort value = *(dataPtr + i);

                        framesArray[_countFrame, block, i] = ushortArr[i];
                    }
                }
                
            }
            _countFrame++;
        }

        public bool IsGrabbing => _transfer?.Grabbing ?? false;
        public int CapturedFrames => _countFrame;
    }
}
