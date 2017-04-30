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

/* pomysły
 * wykrywać palce jako "punkty skupienia". punkty skupienia mają większe znaczenie przy porównywaniu
 * obszary które często się powtarzają mają mniejszą wartość.
 * porównywać głębie
 * wyznaczać wektor głebi
 */

namespace ConfeeDemoWPF
{
    public partial class MainWindow : Window
    {
        [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "CopyMemory")]
        public static extern void CopyMemory(IntPtr destination, IntPtr source, uint length);

        private KinectSensor _kinect;
        private MultiSourceFrameReader _reader;
        private WriteableBitmap _rightHandBitmapSource;
        private WriteableBitmap _leftHandBitmapSource;
        private WriteableBitmap _rightHandColorBitmapSource;
        private WriteableBitmap _leftHandColorBitmapSource;
        private CoordinateMapper _coordinateMapper;
        private KinectGestureRecognizer _gestureRecognizer;

        private int _colorViewWidth = 256;
        private int _colorViewHeight = 256;

        private bool _snapNextTime = false;
        private Random _random = new Random();
        private SpeechSynthesizer _synth = new SpeechSynthesizer();

        private TcpListener _listener;
        private Socket _client;

        public MainWindow()
        {
            InitializeComponent();

            _listener = new TcpListener(IPAddress.Any, 6667);
            _listener.Start();
            _client = _listener.AcceptSocket();

            _kinect = KinectSensor.GetDefault();
            _coordinateMapper = _kinect.CoordinateMapper;
            var depthFrameDesc = _kinect.DepthFrameSource.FrameDescription;

            _gestureRecognizer = new KinectGestureRecognizer(_kinect,
                @"C:\Users\user\Documents\svmtst\classifier2.bin");
            _gestureRecognizer.GestureRecognized += OnGestureRecognized;
            _gestureRecognizer.PreviewFrameArrived += PreviewFrameArrived;
            //_gestureRecognizer.GestureDatabase.CleanDatabase();

            _rightHandView.Source = _rightHandBitmapSource = new WriteableBitmap(
                _gestureRecognizer.DepthFrameWidth, _gestureRecognizer.DepthFrameHeight,
                96.0, 96.0, PixelFormats.Gray8, null);
            _leftHandView.Source = _leftHandBitmapSource = new WriteableBitmap(
                _gestureRecognizer.DepthFrameWidth, _gestureRecognizer.DepthFrameHeight,
                96.0, 96.0, PixelFormats.Gray8, null);
            _rightHandColorView.Source = _rightHandColorBitmapSource = new WriteableBitmap(_colorViewWidth, _colorViewHeight,
                96.0, 96.0, PixelFormats.Bgr32, null);
            _leftHandColorView.Source = _leftHandColorBitmapSource = new WriteableBitmap(_colorViewWidth, _colorViewHeight,
                96.0, 96.0, PixelFormats.Bgr32, null);
            _reader = _kinect.OpenMultiSourceFrameReader(
                FrameSourceTypes.Body |
                FrameSourceTypes.Depth |
                FrameSourceTypes.Color);

            _synth.Rate = 1;


            _reader.MultiSourceFrameArrived += _reader_MultiSourceFrameArrived;
            _kinect.Open();
        }

        private void PreviewFrameArrived(object sender, PreviewFrameArrivedArgs args)
        {
            Dispatcher.Invoke(new Action(() =>
            {
                if (_snapNextTime)
                {
                    _snapNextTime = false;
                    //old version
                    //_gestureRecognizer.GestureDatabase.SaveGesture(args.RightHandFrame, "debug");
                    var libraryPath = @"C:\Users\user\Documents\svmtst\gestures2";
                    var label = _labelTextBox.Text;
                    BitmapLoader.SaveInLibrary(libraryPath, label, args.RightHandFrame,
                        _gestureRecognizer.DepthFrameWidth, _gestureRecognizer.DepthFrameHeight,
                        _gestureRecognizer.DepthFrameWidth, PixelFormat.Format8bppIndexed);
                }

                // draw preview data
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

        //private Timer _gestureRecognizedTimer = null;
        private const string _textToSpeach = "Hello,My,Name is,P,A,W,E,L,Ok";
        private int _wordIndex = 100;
        private DateTime _lastGestureRecognizedTime;
        //private bool _isSpeechCompleted = true;

        private async void OnGestureRecognized(object e, GestureRecognizedArgs args)
        {
            //old
            /*var words = _textToSpeach.Split(',');
            if (_wordIndex < words.Length)
            {
                if (DateTime.Now.Subtract(_lastGestureRecognizedTime).TotalMilliseconds < 300)//
                {
                    return;
                }

                _lastGestureRecognizedTime = DateTime.Now;

                if (args.GestureName != words[_wordIndex])
                {
                    return;
                }

                if(args.GestureName == "Ok")
                {
                    args.GestureName = "Pawel";
                }

                ++_wordIndex;               
            }*/


            args.IsEventCaptured = true;

            var msg = Encoding.UTF8.GetBytes(args.GestureName.ToLower());
            _client.Send(msg);
            System.Diagnostics.Debug.Print("{0} gesture recognized", args.GestureName);

            /* if (_gestureRecognizedTimer != null && _gestureRecognizedTimer.Enabled)
             {
                 _gestureRecognizedTimer.Stop();
             }*/

            /*gestureRecognizedTimer = new Timer(_wordIndex < words.Length ? 1 : 200);
            _gestureRecognizedTimer.Elapsed += delegate
            {
                // speak gesture name
                _synth.SpeakAsyncCancelAll();
                _synth.SpeakAsync(args.GestureName);
            };
            _gestureRecognizedTimer.AutoReset = false;
            _gestureRecognizedTimer.Enabled = true;*/

            _synth.SpeakAsyncCancelAll();
            _synth.SpeakAsync(args.GestureName);

            await Dispatcher.BeginInvoke(new Action(() =>
            {
                _textBlock.Text = args.GestureName;
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

        // TODO multithread
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

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            var checkBox = (CheckBox)sender;
            if (checkBox.IsChecked.Value)
            {
                _wordIndex = 10000;
            }
            else
            {
                _wordIndex = 0;
            }
        }
    }
}
