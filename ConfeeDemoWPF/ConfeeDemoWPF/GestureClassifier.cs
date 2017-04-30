using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Accord.Imaging;
using Accord.IO;
using Accord.MachineLearning;
using Accord.MachineLearning.VectorMachines;
using Accord.MachineLearning.VectorMachines.Learning;
using Accord.Statistics.Kernels;

namespace ConfeeDemoWPF
{
    using BowType = BagOfVisualWords<IFeatureDescriptor<double[]>, double[],
        BinarySplit, HistogramsOfOrientedGradients>;

    class GestureFrame
    {
        public IntPtr Data { get; set; }
        public static int Width => 128;
        public static int Height => 128;
    }

    class GestureLabel
    {
        public string Name { get; set; }
        public int Id { get; set; }
    }

    [Serializable]
    class GestureClassifier
    {
        private BowType _bow;
        private MulticlassSupportVectorMachine<IKernel> _ksvm;
        private List<string> _classLabels;

        public List<string> Labels => _classLabels;

        public static GestureClassifier Load(string filename)
        {
            return Serializer.Load<GestureClassifier>(filename);
        }

        public void Save(string filename)
        {
            Serializer.Save(this, filename);
        }

        public static GestureClassifier Learn(string libraryPath)
        {
            var classifier = new GestureClassifier();
            var bitmaps = BitmapLoader.LoadLibrary(libraryPath);
            classifier.CreateCodebook(bitmaps, out double[][] featureVectors, out int[] classes);
            classifier.Learn(featureVectors, classes);
            return classifier;
        }

        public GestureLabel Classify(GestureFrame frame)
        {
            var featureVector = _bow.Transform(new UnmanagedImage(frame.Data, GestureFrame.Width,
                GestureFrame.Height, GestureFrame.Width, PixelFormat.Format8bppIndexed));
            var label = new GestureLabel();
            label.Id = _ksvm.Decide(featureVector);
            label.Name = _classLabels[label.Id];
            return label;
        }

        private void CreateCodebook(Dictionary<string, List<Bitmap>> bitmaps,
            out double[][] outFeatureVectors, out int[] outClasses)
        {
            _bow = BagOfVisualWords.Create(new HistogramsOfOrientedGradients(), new BinarySplit(1000));
            var trainingData = bitmaps.Values.SelectMany(x => x).ToArray();
            _bow.Learn(trainingData);

            _classLabels = new List<string>();

            var currentClassLabel = 0;
            var featureVectors = new List<double[]>();
            var classLabels = new List<int>();
            foreach (var group in bitmaps)
            {
                foreach (var bitmap in group.Value)
                {
                    classLabels.Add(currentClassLabel);
                    featureVectors.Add(_bow.Transform(bitmap));
                }
                ++currentClassLabel;
                _classLabels.Add(group.Key);
            }

            outFeatureVectors = featureVectors.ToArray();
            outClasses = classLabels.ToArray();
        }

        private void Learn(double[][] featureVectors, int[] classLabels)
        {
            var kernel = Gaussian.Estimate(featureVectors);
            var teacher = new MulticlassSupportVectorLearning<IKernel>()
            {
                Kernel = kernel,
                Learner = (param) => new SequentialMinimalOptimization<IKernel>()
                {
                    Kernel = kernel,
                    Complexity = 100,
                    Tolerance = 0.01,
                    CacheSize = 50000,
                    Strategy = SelectionStrategy.Sequential,
                }
            };
            _ksvm = teacher.Learn(featureVectors, classLabels);
        }
    }
}
