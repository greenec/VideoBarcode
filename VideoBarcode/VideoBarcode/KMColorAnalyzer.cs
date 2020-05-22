using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Trainers;
using OpenCvSharp;

namespace VideoBarcode
{
    class KMColorAnalyzer
    {

        public static Task<ColorSpan> ProcessFrame(Mat<Vec3b> image)
        {
            return Task.Run(() =>
            {
                var mlContext = new MLContext();
                var img = LoadImage(image);

                var fullData = mlContext.Data.LoadFromEnumerable(img.Data);
                var trainingData = mlContext.Data.LoadFromEnumerable(SelectRandom(img.Data, 5000));
                var model = Train(mlContext, trainingData, numberOfClusters: 4);

                if (model == null)
                {
                    //model = Train(mlContext, trainingData, numberOfClusters: 1);
                    return null;
                }

                VBuffer<float>[] centroidsBuffer = default;
                model.Model.GetClusterCentroids(ref centroidsBuffer, out int k);

                var labels = mlContext.Data
                    .CreateEnumerable<Prediction>(model.Transform(fullData), reuseRowObject: false)
                    .ToArray();

                int[] histogram = new int[32];
                foreach (Prediction label in labels)
                {
                    histogram[label.PredictedLabel - 1]++;
                }

                var max = histogram.Select((value, label) => new { value, label })
                                 .OrderByDescending(vi => vi.value)
                                 .ToList();

                float sum = histogram.Sum();


                var cspan = new ColorSpan
                {
                    Color = new List<Color>(),
                    Span = new List<float>()
                };

                for (int i = 0; i < k; i++)
                {
                    var centroid = centroidsBuffer[max[i].label].DenseValues().ToArray();
                    float r = centroid[0] * 255, g = centroid[1] * 255, b = centroid[2] * 255;

                    cspan.Color.Add(Color.FromArgb((int)r, (int)g, (int)b));
                    cspan.Span.Add(max[i].value / sum);
                }

                return cspan;
            });
         }

        private static ClusteringPredictionTransformer<KMeansModelParameters> Train(MLContext mlContext, IDataView data, int numberOfClusters)
        {
            var pipeline = mlContext.Clustering.Trainers.KMeans(numberOfClusters: numberOfClusters);
            ClusteringPredictionTransformer<KMeansModelParameters> model = null;

            try
            {
                Console.WriteLine("Training model...");
                var sw = Stopwatch.StartNew();
                model = pipeline.Fit(data);
                Console.WriteLine("Model trained in {0} ms.", sw.Elapsed.Milliseconds);
            }
            catch (Exception e)
            {
                Console.WriteLine("Too few clusters");
            }

            return model;
        }

        private static ImageEntry LoadImage(Mat<Vec3b> image)
        {
            var indexer = image.GetIndexer();
            var pixels = new PixelEntry[image.Width * image.Height];
            int i = 0;

            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    Vec3b pixel = indexer[y, x];

                    pixels[i++] = new PixelEntry
                    {
                        Features = new[]
                        {
                            (float)(pixel.Item2 / 255f),
                            (float)(pixel.Item1 / 255f),
                            (float)(pixel.Item0 / 255f)
                        }
                    };
                }
            }

            return new ImageEntry
            {
                Data = pixels,
                Width = image.Width,
                Height = image.Height
            };
        }

        private static T[] SelectRandom<T>(T[] array, int count)
        {
            var result = new T[count];
            var rnd = new Random();
            var chosen = new HashSet<int>();

            for (var i = 0; i < count; i++)
            {
                int r;
                while (chosen.Contains((r = rnd.Next(0, array.Length))))
                {
                    continue;
                }

                result[i] = array[r];
            }

            return result;
        }

    }

    public class ColorSpan
    {
        public List<Color> Color { get; set; }
        public List<float> Span { get; set; }
    }

    public class PixelEntry
    {

        [VectorType(3)]
        public float[] Features { get; set; }

    }

    public class ImageEntry
    {

        public PixelEntry[] Data { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

    }

    public class Prediction
    {

        public uint PredictedLabel { get; set; }

    }
}
