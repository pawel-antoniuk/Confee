using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.MediaProperties;
using Windows.Media.SpeechSynthesis;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace Confee
{
    public sealed partial class MainPage : Page
    {

        private MediaCapture _mediaCapture = null;
        private MediaFrameReader _frameReader = null;
        private SoftwareBitmap _colorCamerViewBackBuffer = null;
        private KinectBridgeClient _kinectBridgeClient = null;

        public MainPage()
        {
            InitializeComponent();

            _colorCamerViewBackBuffer = new SoftwareBitmap(BitmapPixelFormat.Yuy2, 1920, 1080);
            _colorCameraView.Source = new SoftwareBitmapSource();

            _kinectBridgeClient = new KinectBridgeClient(6667);
            _kinectBridgeClient.OnMessageReceived += _kinectBridgeClient_OnMessageReceived;
            _kinectBridgeClient.BeginListen();

            CreateFrameReaderAsync();
        }

        private void _kinectBridgeClient_OnMessageReceived(object sender, string msg)
        {
#pragma warning disable CS4014
            Dispatcher.RunAsync(CoreDispatcherPriority.Normal, delegate
            {
                _speechBalloon.Text += msg + " ";
            });
#pragma warning restore CS4014
        }

        private void CreateFrameReaderAsync()
        {
            new Task(async delegate
            {
                var frameSourceGroups = await MediaFrameSourceGroup.FindAllAsync();

                MediaFrameSourceGroup selectedGroup = null;
                MediaFrameSourceInfo colorSourceInfo = null;

                foreach (var sourceGroup in frameSourceGroups)
                {
                    foreach (var sourceInfo in sourceGroup.SourceInfos)
                    {
                        if (sourceInfo.DeviceInformation.Name != "Kinect V2 Video Sensor" ||
                            sourceInfo.MediaStreamType != MediaStreamType.VideoRecord ||
                            sourceInfo.SourceKind != MediaFrameSourceKind.Color)
                        {
                            continue;
                        }
                        colorSourceInfo = sourceInfo;
                        break;
                    }
                    if (colorSourceInfo == null) continue;
                    selectedGroup = sourceGroup;
                    break;
                }

                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                    async () =>
                    {
                        if (selectedGroup == null)
                        {
                            var messageDialog = new MessageDialog("No ready Kinect V2 found!");
                            var okCommand = new UICommand("Ok", cmd => Application.Current.Exit());
                            messageDialog.Commands.Add(okCommand);
                            await messageDialog.ShowAsync();
                        }

                        var settings = new MediaCaptureInitializationSettings()
                        {
                            SourceGroup = selectedGroup,
                            SharingMode = MediaCaptureSharingMode.ExclusiveControl,
                            MemoryPreference = MediaCaptureMemoryPreference.Cpu,
                            StreamingCaptureMode = StreamingCaptureMode.Video
                        };

                        _mediaCapture = new MediaCapture();
                        await _mediaCapture.InitializeAsync(settings);

                        var colorFrameSource = _mediaCapture.FrameSources[colorSourceInfo.Id];
                        var preferredFormat = colorFrameSource.SupportedFormats.FirstOrDefault();

                        await colorFrameSource.SetFormatAsync(preferredFormat);

                        _frameReader = await _mediaCapture.CreateFrameReaderAsync(colorFrameSource, MediaEncodingSubtypes.Yuy2);
                        _frameReader.FrameArrived += FrameArrivedAsync;
                        await _frameReader.StartAsync();
                    });
            }).Start();
        }

        private void _datagramSocket_MessageReceived(DatagramSocket sender,
            DatagramSocketMessageReceivedEventArgs args)
        {
            var reader = args.GetDataReader();
            reader.LoadAsync(64).GetResults();
            var msg = new byte[64];
            reader.ReadBytes(msg);
            System.Diagnostics.Debug.WriteLine(msg);
        }

        private bool _taskRunning = false;

        private void FrameArrivedAsync(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
        {
            var mediaFrameReference = sender.TryAcquireLatestFrame();
            var videoMediaFrame = mediaFrameReference?.VideoMediaFrame;
            var softwareBitmap = videoMediaFrame?.SoftwareBitmap;

            if (softwareBitmap != null)
            {
                if (softwareBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 ||
                    softwareBitmap.BitmapAlphaMode != BitmapAlphaMode.Premultiplied)
                {
                    softwareBitmap = SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
                }

                softwareBitmap = Interlocked.Exchange(ref _colorCamerViewBackBuffer, softwareBitmap);
                softwareBitmap?.Dispose();

                var task = _colorCameraView.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                    async () =>
                    {
                        if (_taskRunning)
                        {
                            return;
                        }
                        _taskRunning = true;

                        SoftwareBitmap latestBitmap;
                        while ((latestBitmap = Interlocked.Exchange(ref _colorCamerViewBackBuffer, null)) != null)
                        {
                            var imageSource = (SoftwareBitmapSource)_colorCameraView.Source;
                            await imageSource.SetBitmapAsync(latestBitmap);
                            latestBitmap.Dispose();
                        }

                        _taskRunning = false;
                    });
            }

            mediaFrameReference.Dispose();

#pragma warning disable CS4014
            Dispatcher.RunAsync(CoreDispatcherPriority.Normal, delegate
            {
                _loadingRing.Visibility = Visibility.Collapsed;
            });
#pragma warning restore CS4014

        }

        private void HamburgerButton_Click(object sender, RoutedEventArgs e)
        {
            MainSplitView.IsPaneOpen = !MainSplitView.IsPaneOpen;
        }

        private CanvasBitmap _backgroundImage = null;
        private CanvasImageBrush _backgroundBrush = null;

        private void _backgroundCanvas_Draw(Microsoft.Graphics.Canvas.UI.Xaml.CanvasControl sender, Microsoft.Graphics.Canvas.UI.Xaml.CanvasDrawEventArgs args)
        {
            var session = args.DrawingSession;
            session.FillRectangle(new Rect(new Point(), sender.RenderSize), _backgroundBrush);
        }

        private void _backgroundCanvas_CreateResources(Microsoft.Graphics.Canvas.UI.Xaml.CanvasControl sender, Microsoft.Graphics.Canvas.UI.CanvasCreateResourcesEventArgs args)
        {
            args.TrackAsyncAction(Task.Run(async () =>
            {
                _backgroundImage = await CanvasBitmap.LoadAsync(sender, new Uri("ms-appx:///Assets/background.png"));
                _backgroundBrush = new CanvasImageBrush(sender, this._backgroundImage);
                _backgroundBrush.ExtendX = _backgroundBrush.ExtendY = CanvasEdgeBehavior.Wrap;
            }).AsAsyncAction());
        }
    }
}
