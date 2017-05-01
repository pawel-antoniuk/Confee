using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using Microsoft.Kinect;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ConfeeDemoWPF
{
    public class GestureRecognizedArgs
    {
        public string GestureName { get; set; }
        public IntPtr GestureImagePointer { get; set; }
        public ulong TrackingId { get; set; }
        public bool IsEventCaptured { get; set; }
        public double Accuracy { get; set; }
    }

    public class PreviewFrameArrivedArgs
    {
        public IntPtr RightHandFrame { get; set; }
        public IntPtr LeftHandFrame { get; set; }
    }

    class GestureRecognizer
    {
        private struct HandDimensions
        {
            public int Corner1X;
            public int Corner1Y;
            public int Corner2X;
            public int Corner2Y;
            public ushort CenterDepthValue;
            public ushort Corner1DepthValue;
            public ushort Corner2DepthValue;
            public int NewImageWidth;
            public int NewImageHeight;
        }
        private class GestureDimensions
        {
            public int OriginalImageWidth;
            public int OriginalImageHeight;
            public HandDimensions RightHand;
            public HandDimensions LeftHand;
        }

        public class ProcessedFrame
        {
            public Mat RightHandFrame;
            public Mat LefttHandFrame;
        }

        public int DepthFrameWidth => 128;
        public int DepthFrameHeight => 128;

        private CoordinateMapper _coordinateMapper;
        private KinectSensor _kinectSensor;
        //private GestureDatabase _gestureDatabase; //old version
        private GestureClassifier _classifier;
        private string _lastGestureName = "";
        private double[] _gestureDensity;

        public event EventHandler<GestureRecognizedArgs> GestureRecognized;
        public event EventHandler<PreviewFrameArrivedArgs> PreviewFrameArrived;
        //public GestureDatabase GestureDatabase => _gestureDatabase; //old version
        public GestureRecognizer(KinectSensor sensor, string databasePath)
        {
            _kinectSensor = sensor;
            _coordinateMapper = _kinectSensor.CoordinateMapper;
            //_classifier = GestureClassifier.Learn(@"C:\Users\user\Documents\svmtst\gestures2");
            //_classifier.Save(databasePath);

            _classifier = GestureClassifier.Load(databasePath);

            _gestureDensity = new double[_classifier.Labels.Count];

            //_gestureDatabase = new GestureDatabase(databasePath, DepthFrameWidth, DepthFrameHeight); //old version
        }
        private HandDimensions FindHandDimensions(Joint handJoint, int originalImageWidth, int originalImageHeight, int baseDistance, IntPtr depthBuffer)
        {
            var handPos = _coordinateMapper.MapCameraPointToDepthSpace(handJoint.Position);
            var centerX = (int)handPos.X;
            var centerY = (int)handPos.Y;
            centerX = Math.Max(centerX, 0);
            centerY = Math.Max(centerY, 0);

            var dims = new HandDimensions()
            {
                NewImageWidth = baseDistance,
                NewImageHeight = baseDistance
            };
            dims.Corner1X = centerX - dims.NewImageWidth / 2;
            dims.Corner1Y = centerY - dims.NewImageHeight / 2;
            dims.Corner1X = Math.Max(dims.Corner1X, 0);
            dims.Corner1Y = Math.Max(dims.Corner1Y, 0);

            if (dims.Corner1X + dims.NewImageWidth >= originalImageWidth)
            {
                dims.Corner1X = originalImageWidth - dims.NewImageWidth - 1;
            }
            if (dims.Corner1Y + dims.NewImageHeight >= originalImageHeight)
            {
                dims.Corner1Y = originalImageHeight - dims.NewImageHeight - 1;
            }

            dims.Corner2X = dims.Corner1X + dims.NewImageWidth;
            dims.Corner2Y = dims.Corner1Y + dims.NewImageHeight;

            if (centerX >= originalImageWidth)
            {
                centerX = originalImageWidth - 1;
            }
            if (centerY >= originalImageHeight)
            {
                centerY = originalImageHeight - 1;
            }
            unsafe
            {
                dims.CenterDepthValue = ((ushort*)depthBuffer)[centerY * originalImageWidth + centerX];
                dims.Corner1DepthValue = ((ushort*)depthBuffer)[dims.Corner1Y * originalImageWidth + dims.Corner1X];
                dims.Corner2DepthValue = ((ushort*)depthBuffer)[dims.Corner2Y * originalImageWidth + dims.Corner2X];
            }

            return dims;
        }
        private GestureDimensions FindDimensions(Body body, IntPtr depthBuffer)
        {
            var spineMidJoint = body.Joints.Single(s => s.Key == JointType.SpineMid).Value;
            var headJoint = body.Joints.Single(s => s.Key == JointType.Head).Value;
            var rightHandJoint = body.Joints.Single(s => s.Key == JointType.HandRight).Value;
            var leftHandJoint = body.Joints.Single(s => s.Key == JointType.HandLeft).Value;

            var spineMidPos = _coordinateMapper.MapCameraPointToDepthSpace(spineMidJoint.Position);
            var headPos = _coordinateMapper.MapCameraPointToDepthSpace(headJoint.Position);

            var gestureDims = new GestureDimensions()
            {
                OriginalImageWidth = _kinectSensor.DepthFrameSource.FrameDescription.Width,
                OriginalImageHeight = _kinectSensor.DepthFrameSource.FrameDescription.Height,
            };
            var baseDistance = Math.Abs((int)((headPos.Y - spineMidPos.Y) * 1.25));

            gestureDims.RightHand = FindHandDimensions(rightHandJoint, gestureDims.OriginalImageWidth,
                gestureDims.OriginalImageHeight, baseDistance, depthBuffer);
            gestureDims.LeftHand = FindHandDimensions(leftHandJoint, gestureDims.OriginalImageWidth,
                gestureDims.OriginalImageHeight, baseDistance, depthBuffer);

            return gestureDims;
        }
        private Mat ProcessHandFrame(HandDimensions dims, IntPtr depthData, int originalImageWidth, int originalImageHeight)
        {
            Mat inputImage;
            Mat outputImage;
            {
                inputImage = new Mat(originalImageHeight, originalImageWidth, DepthType.Cv16U, 1, depthData,
                    originalImageWidth * 2);
            }
            {
                inputImage = new Mat(inputImage, new Rectangle(dims.Corner1X, dims.Corner1Y,
                    dims.NewImageWidth, dims.NewImageHeight));
                outputImage = new Mat();
                CvInvoke.Resize(inputImage, outputImage, new Size(DepthFrameWidth, DepthFrameHeight), 0, 0, Inter.Nearest);
                inputImage = outputImage;
            }
            {
                outputImage = new Mat();
                inputImage.ConvertTo(outputImage, DepthType.Cv32F, 1.0 / 65535.0);
                inputImage = outputImage;
            }

            {
                outputImage = new Mat();
                var depthScalarValue = dims.CenterDepthValue / 65535.0;
                CvInvoke.InRange(inputImage, new ScalarArray(depthScalarValue - 0.005), new ScalarArray(depthScalarValue + 0.001), outputImage);
                inputImage = outputImage;
            }
            {
                outputImage = new Mat();
                inputImage.ConvertTo(outputImage, DepthType.Cv8U, 255.0);
                inputImage = outputImage;
            }
            {
                var contours = new VectorOfVectorOfPoint();
                var inputImageCopy = new Mat();
                inputImage.CopyTo(inputImageCopy);
                CvInvoke.FindContours(inputImageCopy, contours, null /*hierarchy*/, RetrType.List, ChainApproxMethod.ChainApproxNone);
                int contoursIndex = 0;

                int maxArea = 0;
                for (int i = 0; i < contours.Size; ++i)
                {
                    var rect = CvInvoke.BoundingRectangle(contours[i]);
                    var area = rect.Width * rect.Height;
                    if (area > maxArea)
                    {
                        maxArea = area;
                        contoursIndex = i;
                    }
                }

                if (contours.Size == 0)
                {
                    return null;
                }

                var contourRect = CvInvoke.BoundingRectangle(contours[contoursIndex]);
                var roi = new Rectangle();
                if (contourRect.Width > contourRect.Height)
                {
                    roi.X = 0;
                    roi.Y = 0;
                    roi.Width = DepthFrameWidth;
                    roi.Height = (int)((double)contourRect.Height / contourRect.Width * roi.Width);
                }
                else
                {
                    roi.X = 0;
                    roi.Y = 0;
                    roi.Height = DepthFrameHeight;
                    roi.Width = (int)((double)contourRect.Width / contourRect.Height * roi.Height);
                }

                outputImage = new Mat(DepthFrameHeight, DepthFrameWidth, DepthType.Cv8U, 1);
                outputImage.SetTo(new MCvScalar(0));
                CvInvoke.Resize(new Mat(inputImage, contourRect), new Mat(outputImage, roi), new Size(roi.Width, roi.Height));

                /*var diffMat = new Mat();
                var whiteMat = new Mat(DepthFrameHeight, DepthFrameWidth, DepthType.Cv8U, 1);
                whiteMat.SetTo(new MCvScalar(255));
                CvInvoke.AbsDiff(outputImage, whiteMat, diffMat);
                if (CvInvoke.Sum(diffMat).V0 == 0)
                {
                    contourRect = CvInvoke.BoundingRectangle(contours[1]);
                }*/

                inputImage = outputImage;
            }
            {
                //old version
                //CvInvoke.GaussianBlur(inputImage, outputImage, new Size(11, 11), 20);
            }

            return outputImage;
        }
        private ProcessedFrame ProcessFrame(GestureDimensions dims, IntPtr depthBuffer)
        {
            var frame = new ProcessedFrame()
            {
                RightHandFrame = ProcessHandFrame(dims.RightHand, depthBuffer, dims.OriginalImageWidth, dims.OriginalImageHeight),
                LefttHandFrame = ProcessHandFrame(dims.LeftHand, depthBuffer, dims.OriginalImageWidth, dims.OriginalImageHeight)
            };
            return frame;
        }
        public void InputFrames(DepthFrame depthFrame, Body trackedBody)
        {
            ProcessedFrame frame = null;
            using (var depthBuffer = depthFrame.LockImageBuffer())
            {
                var gestureDims = FindDimensions(trackedBody, depthBuffer.UnderlyingBuffer);
                frame = ProcessFrame(gestureDims, depthBuffer.UnderlyingBuffer);
            }

            if (frame.RightHandFrame == null ||
                frame.LefttHandFrame == null ||
                frame.RightHandFrame.IsEmpty ||
                frame.LefttHandFrame.IsEmpty)
            {
                return;
            }

            var trackedBodyId = trackedBody.TrackingId;

            ThreadPool.QueueUserWorkItem(delegate
            {
                var previewFrameArrivedArgs = new PreviewFrameArrivedArgs()
                {
                    RightHandFrame = frame.RightHandFrame.DataPointer,
                    LeftHandFrame = frame.LefttHandFrame.DataPointer
                };
                PreviewFrameArrived(this, previewFrameArrivedArgs);

                //var bestMatch = _gestureDatabase.MatchGesture(frame.RightHandFrame, GestureDatabase.GestureType.RightHand); //old version

                var classifierFrame = new GestureFrame();
                classifierFrame.Data = frame.RightHandFrame.DataPointer;
                var classifiedLabel = _classifier.Classify(classifierFrame);


                _gestureDensity[classifiedLabel.Id] += 1;

                double maxDensity = 0;
                var maxDensityIndex = 0;

                for (var i = 0; i < _gestureDensity.Length; ++i)
                {
                    _gestureDensity[i] *= 0.8;

                    if (!(_gestureDensity[i] > maxDensity)) continue;
                    maxDensity = _gestureDensity[i];
                    maxDensityIndex = i;
                }

                var bestMatchGestureName = _classifier.Labels[maxDensityIndex];

                //old version
                /*if (bestMatch == null || bestMatch.GestureType == null) return;
                if (_lastGestureName != bestMatch.GestureType.Name)
                {
                    var args = new GestureRecognizedArgs
                    {
                        GestureName = bestMatch.GestureType.Name,
                        GestureImagePointer = frame.RightHandFrame,
                        TrackingId = trackedBodyId,
                        IsEventCaptured = false,
                    };
                    GestureRecognized(this, args);
                    if (args.IsEventCaptured)
                    {
                        _lastGestureName = bestMatch.GestureType.Name;
                    }
                }*/

                if (_lastGestureName == bestMatchGestureName) return;
                var args = new GestureRecognizedArgs
                {
                    GestureName = bestMatchGestureName,
                    GestureImagePointer = frame.RightHandFrame,
                    TrackingId = trackedBodyId,
                    IsEventCaptured = false,
                    Accuracy = maxDensity,
                };
                GestureRecognized(this, args);
                if (args.IsEventCaptured)
                {
                    _lastGestureName = bestMatchGestureName;
                }
            });
        }
    }
}
