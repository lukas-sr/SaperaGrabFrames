using System;
using System.IO;
using System.Runtime.InteropServices;
using DALSA.SaperaLT.Examples.NET.Utils;
using DALSA.SaperaLT.SapClassBasic;

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
        private MyAcquisitionParams _acqParams;
        private byte _countFrame;
        public ushort[,,] framesArray;
        public byte numFrames;
        public ushort blockSize { get; private set; }
        public enum CameraModel
        {
            Xtium1,
            Xtium2
        }
        public CameraModel model;
        public const int byteTreshold = 8;

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

            InitializeCameraResources(serverName);
        }

        private void InitializeCameraResources(string serverName)
        {
            model = IdentifyCameraModel(serverName);

            if ((model != CameraModel.Xtium1) && (model != CameraModel.Xtium2))
            {
                throw new NotSupportedException($"Unsupported camera model: {serverName}");
            }
        }

        private CameraModel IdentifyCameraModel(string serverName)
        {
            CameraModel cameraModel;

            if (serverName == "Xtium-CLHS_PX8_1") cameraModel = CameraModel.Xtium1;

            else if (serverName == "Xtium2-CLHS_PX8_1") cameraModel = CameraModel.Xtium2;

            else throw new ArgumentException($"Unsupported camera: {serverName}");

            return cameraModel;
        }
        public void InitializeFrameArray(byte dim1, int dim2, int dim3)
        {
            if (dim1 == 0 || dim2 <= 0 || dim3 <= 0) throw new ArgumentException("Invalid array dimensions");

            framesArray = new ushort[dim1, dim2, dim3];
        }

        public unsafe void ProcessFrameBuffer(IntPtr bufferAddress, uint bufferSize)
        {
            if (bufferAddress == IntPtr.Zero)
                throw new ArgumentNullException(nameof(bufferAddress));

            blockSize = (ushort)_buffers.Width;

            var numBlocks = (int)Math.Ceiling((double)bufferSize / blockSize);
            var blockSizeBytes = blockSize * sizeof(ushort);

            for (var block = 0; block < numBlocks; block++)
            {
                var sourceAddress = bufferAddress + (block * blockSizeBytes * sizeof(ushort));
                var buffer = new short[blockSize];

                Marshal.Copy(sourceAddress, buffer, 0, blockSize);

                for (var i = 0; i < blockSize; i++) framesArray[_countFrame, block, i] = unchecked((ushort)buffer[i]);
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

            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Configuration failed", ex);
            }
        }
        public void ConfigureTransferEvents()
        {
            _transfer.StartMode = SapTransfer.XferStartMode.Synchronous;
        }

        public void TransferCallback(object sender, SapXferNotifyEventArgs e)
        {
            if (_transfer == null || _buffers == null) return;

            object[] contextContent = e.Context as object[];
            SapView view = contextContent[0] as SapView;
            SapBuffer pBufferAcq = contextContent[1] as SapBuffer;

            try
            {
                if (_buffers.GetAddress(out IntPtr address))
                {
                    ProcessFrameBuffer(address, (uint)_buffers.Width);
                }
            }
            catch (Exception ex)
            {
                throw new ArgumentException("Message:", nameof(ex.Message));
            }

        }
        public void CreateObjects()
        {
            if (!_acq.Create() || !_buffers.Create() || !_transfer.Create())
            {
                DestroyAll();
                return;
            }

            blockSize = (ushort)_buffers.Width;
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
                if (_buffers.GetAddress(out IntPtr buffAddr)) SaveFrameArray((UInt16)(_buffers.Width), (UInt16)(_buffers.Height), buffAddr);

                else Console.WriteLine("Error accessing buffer!");
            }
            return framesArray;
        }

        public void DestroyAll()
        {
            if (_transfer != null)
            {
                _transfer.Destroy();
                _transfer.Dispose();
            }

            if (_buffers != null)
            {
                _buffers.Destroy();
                _buffers.Dispose();
            }

            if (_acqDevice != null)
            {
                _acqDevice.Destroy();
                _acqDevice.Dispose();
            }

            if (_acq != null)
            {
                _acq.Destroy();
                _acq.Dispose();
            }


            if (_view != null)
            {
                _view.Destroy();
                _view.Dispose();
            }
        }

        private unsafe void SaveFrameArray(ushort width, ushort height, IntPtr buffAddress)
        {
            for (byte block = 0; block < height; block++)
            {
                if (!_buffers.GetParameter(SapBuffer.Prm.PIXEL_DEPTH, out int bytePixel)) { bytePixel = 8; }
               
                if (bytePixel > byteTreshold)
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

                    for (int i = 0; i < blockSize; i++) framesArray[_countFrame, block, i] = *(dataPtr + i);
                }

            }
            _countFrame++;
        }

        public bool IsGrabbing => _transfer?.Grabbing ?? false;
        public int CapturedFrames => _countFrame;
    }
}