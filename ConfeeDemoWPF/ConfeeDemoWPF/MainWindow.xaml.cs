using Emgu.CV;
using Emgu.CV.CvEnum;
using Microsoft.Kinect;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Speech.Synthesis;
using System.Text;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PixelFormat = System.Drawing.Imaging.PixelFormat;

namespace ConfeeDemoWPF
{
    public partial class MainWindow : Window
    {
        [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "CopyMemory")]
        public static extern void CopyMemory(IntPtr destination, IntPtr source, uint length);

        private readonly KinectSensor _kinect;
        private readonly MultiSourceFrameReader _reader;
        private readonly WriteableBitmap _rightHandBitmapSource;
        private readonly WriteableBitmap _leftHandBitmapSource;
        private readonly WriteableBitmap _rightHandColorBitmapSource;
        private readonly WriteableBitmap _leftHandColorBitmapSource;
        private readonly CoordinateMapper _coordinateMapper;
        private readonly GestureRecognizer _gestureRecognizer;
        private readonly SpeechRecognizer _speechRecognizer;
        private readonly KinectBridgeServer _kinectBridgeServer;

        private int _colorViewWidth = 256;
        private int _colorViewHeight = 256;

        private bool _snapNextTime = false;
        private Random _random = new Random();
        private readonly SpeechSynthesizer _synth = new SpeechSynthesizer();

        

        public MainWindow()
        {
            InitializeComponent();

            _kinectBridgeServer = new KinectBridgeServer(6667);

            _kinect = KinectSensor.GetDefault();
            _coordinateMapper = _kinect.CoordinateMapper;
            var depthFrameDesc = _kinect.DepthFrameSource.FrameDescription;

            _gestureRecognizer = new GestureRecognizer(_kinect,
                @"..\..\..\..\Gestures", false);
            _gestureRecognizer.GestureRecognized += OnGestureRecognized;
            _gestureRecognizer.PreviewFrameArrived += PreviewFrameArrived;
            //_gestureRecognizer.GestureDatabase.CleanDatabase();

            RightHandView.Source = _rightHandBitmapSource = new WriteableBitmap(
                _gestureRecognizer.DepthFrameWidth, _gestureRecognizer.DepthFrameHeight,
                96.0, 96.0, PixelFormats.Gray8, null);
            LeftHandView.Source = _leftHandBitmapSource = new WriteableBitmap(
                _gestureRecognizer.DepthFrameWidth, _gestureRecognizer.DepthFrameHeight,
                96.0, 96.0, PixelFormats.Gray8, null);
            RightHandColorView.Source = _rightHandColorBitmapSource = new WriteableBitmap(_colorViewWidth, _colorViewHeight,
                96.0, 96.0, PixelFormats.Bgr32, null);
            LeftHandColorView.Source = _leftHandColorBitmapSource = new WriteableBitmap(_colorViewWidth, _colorViewHeight,
                96.0, 96.0, PixelFormats.Bgr32, null);
            _reader = _kinect.OpenMultiSourceFrameReader(
                FrameSourceTypes.Body |
                FrameSourceTypes.Depth |
                FrameSourceTypes.Color);

            _synth.Rate = 1;

            _reader.MultiSourceFrameArrived += _reader_MultiSourceFrameArrived;
            _kinect.Open();

            var audioStream = _kinect.AudioSource.AudioBeams[0].OpenInputStream();
            _speechRecognizer = new SpeechRecognizer(audioStream);
            _speechRecognizer.SpeechRecognized += _speechRecognizer_SpeechRecognized;
        }

        private void _speechRecognizer_SpeechRecognized(object sender, string e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                SpeechText.Content = e;
            }));
        }

        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
        }

        private void PreviewFrameArrived(object sender, PreviewFrameArrivedArgs args)
        {
            Dispatcher.Invoke(new Action(() =>
            {
                if (_snapNextTime)
                {
                    _snapNextTime = false;
                    //_gestureRecognizer.GestureDatabase.SaveGesture(args.RightHandFrame, "debug");
                    var libraryPath = @"..\..\..\..\Gestures\gestures";
                    var label = _labelTextBox.Text;
                    BitmapLoader.SaveInLibrary(libraryPath, label, args.RightHandFrame,
                        _gestureRecognizer.DepthFrameWidth, _gestureRecognizer.DepthFrameHeight,
                        _gestureRecognizer.DepthFrameWidth, PixelFormat.Format8bppIndexed);
                }

                // draw preview frame
                var imageWidth = _gestureRecognizer.DepthFrameWidth;
                var imageHeight = _gestureRecognizer.DepthFrameHeight;
                var imageSize = imageWidth * imageHeight;

                _rightHandBitmapSource.Lock();
                CopyMemory(_rightHandBitmapSource.BackBuffer, args.RightHandFrame, (uint)imageSize);
                _rightHandBitmapSource.AddDirtyRect(new Int32Rect(0, 0, imageWidth, imageHeight));
                _rightHandBitmapSource.Unlock();

                _leftHandBitmapSource.Lock();
                CopyMemory(_leftHandBitmapSource.BackBuffer, args.LeftHandFrame, (uint)imageSize);
                _leftHandBitmapSource.AddDirtyRect(new Int32Rect(0, 0, imageWidth, imageHeight));
                _leftHandBitmapSource.Unlock();
            }));
        }

        private async void OnGestureRecognized(object e, GestureRecognizedArgs args)
        {
            args.IsEventCaptured = true;
            _kinectBridgeServer.BroadcastMessage(args.GestureName.ToLower());
            System.Diagnostics.Debug.Print("{0} gesture recognized", args.GestureName);

            _synth.SpeakAsyncCancelAll();
            _synth.SpeakAsync(args.GestureName);

            await Dispatcher.BeginInvoke(new Action(() =>
            {
                TextBlock.Text = args.GestureName;
                AccuracyLabel.Content = args.Confidence.ToString("F");
            }));
        }

        private void DrawJointNearestArea(Joint hand, ColorFrame colorFrame, WriteableBitmap target)
        {
            var colorFrameDesc = _kinect.ColorFrameSource.FrameDescription;

            var colorCenter = _coordinateMapper.MapCameraPointToColorSpace(hand.Position);
            var colorCornerX = (int)(colorCenter.X - _colorViewWidth / 2);
            var colorCornerY = (int)(colorCenter.Y - _colorViewHeight / 2);

            colorCornerX = Math.Max(colorCornerX, 0);
            colorCornerY = Math.Max(colorCornerY, 0);

            if (colorCornerX + _colorViewWidth >= colorFrameDesc.Width)
            {
                colorCornerX = colorFrameDesc.Width - _colorViewWidth - 1;
            }
            if (colorCornerY + _colorViewHeight >= colorFrameDesc.Height)
            {
                colorCornerY = colorFrameDesc.Height - _colorViewHeight - 1;
            }

            var colorMat = new Mat(colorFrameDesc.Height, colorFrameDesc.Width, DepthType.Cv8U, 4);
            colorFrame.CopyConvertedFrameDataToIntPtr(colorMat.DataPointer,
                (uint)(colorFrameDesc.Width * colorFrameDesc.Height * 4), ColorImageFormat.Bgra);

            var croppedColorMat = new Mat(colorMat, new System.Drawing.Rectangle(colorCornerX, colorCornerY,
                _colorViewWidth, _colorViewHeight));

            var resizedColorMat = new Mat();
            CvInvoke.Resize(croppedColorMat, resizedColorMat, new System.Drawing.Size(_colorViewWidth, _colorViewHeight),
                0, 0, Inter.Nearest);

            target.Lock();
            CopyMemory(target.BackBuffer, resizedColorMat.DataPointer, (uint)(_colorViewWidth * _colorViewHeight * 4));
            target.AddDirtyRect(new Int32Rect(0, 0, _colorViewWidth, _colorViewHeight));
            target.Unlock();
        }

        private void _reader_MultiSourceFrameArrived(object sender, MultiSourceFrameArrivedEventArgs e)
        {
            DepthFrame depthFrame = null;
            BodyFrame bodyFrame = null;
            ColorFrame colorFrame = null;

            try
            {
                var frame = e.FrameReference.AcquireFrame();
                if (frame == null) return;

                // get depth frame
                depthFrame = frame.DepthFrameReference.AcquireFrame();
                if (depthFrame == null) return;

                // get body frame
                bodyFrame = frame.BodyFrameReference.AcquireFrame();
                if (bodyFrame == null) return;

                // get color frame
                colorFrame = frame.ColorFrameReference.AcquireFrame();
                if (colorFrame == null) return;

                // get tracked body
                var bodies = new Body[bodyFrame.BodyCount];
                bodyFrame.GetAndRefreshBodyData(bodies);
                var trackedBody = bodies.FirstOrDefault(s => s.IsTracked);
                if (trackedBody == null) return;

                // send input to recognizer
                _gestureRecognizer.InputFrames(depthFrame, trackedBody);

                // draw color frames
                var rightHandJoint = trackedBody.Joints.Single(s => s.Key == JointType.HandRight).Value;
                var leftHandJoint = trackedBody.Joints.Single(s => s.Key == JointType.HandLeft).Value;
                DrawJointNearestArea(rightHandJoint, colorFrame, _rightHandColorBitmapSource);
                DrawJointNearestArea(leftHandJoint, colorFrame, _leftHandColorBitmapSource);
            }
            finally
            {
                depthFrame?.Dispose();
                bodyFrame?.Dispose();
                colorFrame?.Dispose();
            }
        }

        private void _button_Click(object sender, RoutedEventArgs e)
        {
            _snapNextTime = true;
        }
    }
}
