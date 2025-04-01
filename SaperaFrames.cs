using System;
using System.IO;
using System.Collections;
using System.Runtime.InteropServices;
using DALSA.SaperaLT.SapClassBasic;
using DALSA.SaperaLT.Examples.NET.Utils;
using System.Threading;

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
        public byte numFrames { get; }
        private byte _countFrame;
        private const int MaxTime = 255;
        public ushort blockSize { get; private set; }
        private enum CameraModel
        {
            XtiumCLHSPx8_1,
            Xtium2CLHSPx8_1
        }

        public SaperaFrames(string serverName, string filePath, byte nFrames)
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

            numFrames = nFrames;
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
        private void InitializeFrameArray(byte dim1, int dim2, int dim3)
        {
            if (dim1 == 0 || dim2 <= 0 || dim3 <= 0)
                throw new ArgumentException("Invalid array dimensions");

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
        public void CreateObjects()
        {
            if (!_acq.Create() || !_buffers.Create() || !_transfer.Create() || !_view.Create())
            {
                Console.WriteLine("Error during object creation");
                Destroy(_acq, _acqDevice, _buffers, _transfer, _view);
                return;
            }
            InitializeFrameArray(numFrames, _buffers.Height, _buffers.Width);
        }

        public void StartGrabbing()
        {
            if (_transfer == null) return;

            for (uint i = 0; i < numFrames; i++)
            {
                _transfer.Snap();
                _transfer.Wait(MaxTime);
                Thread.Sleep(100);
            }

            ProcessCapturedFrames();

            Destroy(_acq, _acqDevice, _buffers, _transfer, _view);
        }

        public void ProcessCapturedFrames()
        {
            for (byte i = 0; i < numFrames; i++)
            {
                if (_buffers.GetAddress(out IntPtr buffAddress))
                {
                    SaveFrameArray((UInt16)(_buffers.Width), (UInt16)(_buffers.Height), buffAddress);
                }
                else
                {
                    Console.WriteLine("Error accessing buffer!");
                }
            }
        }

        public void Destroy(SapAcquisition acq, SapAcqDevice camera, SapBuffer buf, SapTransfer xfer, SapView view)
        {

            if (xfer != null)
            {
                xfer.Destroy();
                xfer.Dispose();
            }

            if (buf != null)
            {
                buf.Destroy();
                buf.Dispose();
            }


            if (camera != null)
            {
                camera.Destroy();
                camera.Dispose();
            }

            if (acq != null)
            {
                acq.Destroy();
                acq.Dispose();
            }


            if (view != null)
            {
                view.Destroy();
                view.Dispose();
            }
        }

        public void SaveFrameArray(UInt16 width, UInt16 height, IntPtr buffAddress)
        {

            int[] intArr = new int[blockSize];

            for (byte block = 0; block < height; block++)
            {
                Marshal.Copy(buffAddress + (block * blockSize * sizeof(UInt16)), intArr, 0, blockSize);

                for (int i = 0; i < blockSize; i++)
                {
                    framesArray[_countFrame, block, i] = (UInt16)intArr[i];
                }
            }
            _countFrame++;
        }

        public bool IsGrabbing => _transfer?.Grabbing ?? false;
        public int CapturedFrames => _countFrame;
    }
}
