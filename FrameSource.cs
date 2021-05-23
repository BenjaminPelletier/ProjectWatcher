using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OpenCvSharp;

namespace ProjectWatcher
{
    class FrameSource : IDisposable
    {
        private VideoCapture _Capture;
        private bool _Capturing = false;
        private Thread _Thread;

        public class NewFrameEventArgs : EventArgs
        {
            public readonly Mat Frame;

            public NewFrameEventArgs(Mat frame)
            {
                this.Frame = frame;
            }
        }

        public event EventHandler<NewFrameEventArgs> NewFrame;
        public event EventHandler CameraFailed;

        public FrameSource(int camera, int width = 10000, int height = 10000)
        {
            _Capture = new VideoCapture(camera);
            _Capture.FrameWidth = width;
            _Capture.FrameHeight = height;
        }

        public void Start()
        {
            if (!_Capturing)
            {
                _Capturing = true;
                _Thread = new Thread(this.CaptureLoop);
                _Thread.Start();
            }
        }

        public void Stop()
        {
            _Capturing = false;
        }

        private void CaptureLoop()
        {
            using (Mat image = new Mat()) {
                while (_Capturing)
                {
                    bool success = _Capture.Read(image);
                    if (success)
                    {
                        NewFrame?.Invoke(this, new NewFrameEventArgs(image));
                    }
                    else
                    {
                        CameraFailed?.Invoke(this, EventArgs.Empty);
                        _Capturing = false;
                    }
                }

                _Capture.Dispose();
            }
        }

        public void Dispose()
        {
            _Capturing = false;
        }
    }
}
