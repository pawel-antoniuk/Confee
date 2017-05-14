using Emgu.CV;
using Emgu.CV.CvEnum;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ConfeeDemoWPF
{
    class GestureDatabase
    {
        private readonly List<Gesture> _inputGestures = new List<Gesture>();
        private readonly Random _random = new Random();
        private readonly string _databaseDirectory;
        private const string _fileExtension = ".raw";
        private readonly int _frameWidth;
        private readonly int _frameHeight;
        private readonly int _frameSize;
        private LinkedList<GestureCacheEntry> _gestureCache = new LinkedList<GestureCacheEntry>();
        private const int _maxCacheSize = 30;

        public class GestureFrame
        {
            public int FrameId;
            public Mat Data;
        }
        public class Gesture
        {
            public int Id;
            public string Name;
            public List<GestureFrame> Frames;
        }

        public class GestureOccurrence
        {
            public Gesture GestureType;
            public double MatchValue;
        }

        public class GestureCacheEntry
        {
            public double[] PreviousMatchedValues;
        }

        public enum GestureType
        {
            RightHand,
            LeftHand
        }

        public GestureDatabase(string databaseDirectory, int frameWidth, int frameHeight)
        {
            _databaseDirectory = databaseDirectory;
            _frameWidth = frameWidth;
            _frameHeight = frameHeight;
            _frameSize = _frameWidth * _frameHeight;
            LoadDatabase();
        }

        private void LoadDatabase()
        {
            int nextId = 1;
            var byteArray = new byte[_frameSize];
            foreach (var categoryDirectory in Directory.GetDirectories(_databaseDirectory))
            {
                var gesture = new Gesture()
                {
                    Id = nextId,
                    Frames = new List<GestureFrame>(),
                    Name = Path.GetFileNameWithoutExtension(categoryDirectory)
                };
                ++nextId;

                foreach (var gestureFilename in Directory.GetFiles(categoryDirectory))
                {
                    using (var reader = new BinaryReader(File.Open(gestureFilename, FileMode.Open)))
                    {
                        reader.Read(byteArray, 0, _frameSize);
                    }
                    unsafe
                    {
                        fixed (byte* pData = byteArray)
                        {
                            var readMat = new Mat(_frameHeight, _frameWidth, DepthType.Cv8U, 1);
                            Marshal.Copy(byteArray, 0, readMat.DataPointer, _frameSize);
                            var frame = new GestureFrame();
                            frame.Data = readMat;
                            frame.FrameId = Convert.ToInt32(Path.GetFileNameWithoutExtension(gestureFilename));
                            gesture.Frames.Add(frame);
                        }
                    }
                }

                _inputGestures.Add(gesture);
            }
        }

        public void SaveGesture(IntPtr dataPointer, string gestureName, string filename)
        {
            var path = _databaseDirectory + gestureName;
            Directory.CreateDirectory(path);
            path += "\\" + filename + _fileExtension;
            using (var writer = new BinaryWriter(File.Open(path, FileMode.OpenOrCreate)))
            {
                var bytesArray = new byte[_frameSize];
                Marshal.Copy(dataPointer, bytesArray, 0, _frameSize);
                writer.Write(bytesArray);
            }
        }
        public void SaveGesture(IntPtr dataPointer, string gestureName)
        {
            SaveGesture(dataPointer, gestureName, _random.Next().ToString());
        }

        public GestureOccurrence MatchGesture(Mat inputImage, GestureType type)
        {
            Gesture bestMatch = null;
            var bestMatchValue = double.MaxValue;
            var workingMat = new Mat();
            // prepare cache to write
            /*if (_gestureCache.Count >= _maxCacheSize)
            {
                _gestureCache.RemoveFirst();
            }
            var currentCacheEntry = new GestureCacheEntry();
            currentCacheEntry.PreviousMatchedValues = new double[_inputGestures.Count];*/

            // find the best and the worst value
            for (int i = 0, iLen = _inputGestures.Count; i < iLen; ++i)
            {
                var gesture = _inputGestures[i];
                // TODO replace an average with 
                // find an average match value of this gesture
                //double sumOfMatches = 0;
                double bestImageMatchValue = double.MaxValue;
                foreach (var frame in gesture.Frames)
                {
                    var matchValue = CompareMats(inputImage, frame.Data, workingMat);
                    if(matchValue < bestImageMatchValue)
                    {
                        bestImageMatchValue = matchValue;
                    }
                    //sumOfMatches += CompareMats(inputImage, img);
                }

                // add entry to cache
                //currentCacheEntry.PreviousMatchedValues[i] = sumOfMatches / gesture.Data.Count;

                // add an average of previous occurrences
                /*int numberOfOccurrences = 0;
                foreach (var cacheEntry in _gestureCache)
                {
                    sumOfMatches += cacheEntry.PreviousMatchedValues[i];
                    ++numberOfOccurrences;
                }
                var averageMatch = sumOfMatches / (gesture.Data.Count + numberOfOccurrences); // TODO division by 0
                */


                // check if best match
                //if (averageMatch < bestMatchValue)
                if (bestImageMatchValue < bestMatchValue)
                {
                    bestMatchValue = bestImageMatchValue;
                    bestMatch = gesture;
                }
            }

            return new GestureOccurrence
            {
                GestureType = bestMatch,
                MatchValue = bestMatchValue,
            };
        }

        public void CleanDatabase()
        {
            var workingMat = new Mat();
            foreach(var gesture in _inputGestures)
            {
                /*double minDiff = double.MaxValue;
                int minId1 = 0;
                int minId2 = 0;*/
                int cleanCounter = 0;
                for (int i = 0; i < gesture.Frames.Count; ++i)
                {
                    for (int j = i + 1; j < gesture.Frames.Count;)
                    {
                        var diff = CompareMats(gesture.Frames[i].Data, gesture.Frames[j].Data, workingMat);
                        if(diff < 40000)
                        {
                            ++cleanCounter;
                            /*minDiff = diff;
                            minId1 = gesture.Frames[i].FrameId;
                            minId2 = gesture.Frames[j].FrameId;*/
                            gesture.Frames.RemoveAt(j);
                        }
                        else
                        {
                            ++j;
                        }
                    }
                }

                System.Diagnostics.Debug.Print(string.Format("Removed {1} frames in {0} gesture", gesture.Name, cleanCounter));
            }
        }

        public double CompareMats(Mat m1, Mat m2, Mat workingMat)
        {
            CvInvoke.AbsDiff(m1, m2, workingMat);
            return CvInvoke.Sum(workingMat).V0;
        }
    }
}
