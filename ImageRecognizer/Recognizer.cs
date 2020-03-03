using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Drawing;
using System.Threading;
using System.Numerics.Tensors;
using Microsoft.ML.OnnxRuntime;
using ImageReadCS;
using DataBase;
using System.Collections;

namespace ImageRecognizer
{
    public class Recognizer
    {
        private string modelPath;
        private List<float[]> images;
        private Thread[] threads;
        public ConcurrentQueue<Tuple<string, int>> results = new ConcurrentQueue<Tuple<string, int>>();
        private ConcurrentQueue<float[]> jobs;
        private int numProcs = 1;//Environment.ProcessorCount - 1;
        private AutoResetEvent autoFinishEvent = new AutoResetEvent(false);
        private string[] imgDir;
        private Thread finish_thread;
        bool alive = false;

        public Recognizer(string mPath)
        {
            modelPath = mPath;
        }

        public void InitializePath(string pathDir)
        {
            imgDir = Directory.GetFiles(pathDir).Where(a => a.Split("/").Last()[0] != '.').ToArray();
            int count = imgDir.Count();
            images = new List<float[]>();
            for (int i = 0; i < count; i++)
            {
                Bitmap img = new Bitmap(imgDir[i]);
                GrayscaleFloatImage a = ImageIO.BitmapToGrayscaleFloatImage(img);
                images.Add(new float[a.rawdata.Length]);
                a.rawdata.CopyTo(images[i], 0);
            }
           
            threads = new Thread[numProcs];
            jobs = new ConcurrentQueue<float[]>(images);
        }
       
        public void DoSession()
        {
            alive = true;

            for (int i = 0; i < numProcs; i++)
            {
                threads[i] = new Thread((pi) => DoJobs((int)pi));
                threads[i].Start(i);
            }

            for (int i = 0; i < numProcs; i++)
                threads[i].Join();
        }

        public void DoJobs(int number)
        {
            while (!jobs.IsEmpty)
            {
                if (!alive) return;
                if (jobs.TryDequeue(out float[] image))
                {
                    Console.WriteLine("tried!");
                    ProcessFile(image);
                    Console.WriteLine("processed!");
                }
            }
        }

        public void ProcessFile(float[] image)
        {
            int resNumb = -1;
            if (DataBaseContains(image, ref resNumb))
            {
                results.Enqueue(new Tuple<string, int>(imgDir[images.IndexOf(image)], resNumb));
                return;
            }

            SessionOptions options = new SessionOptions();
            options.SetSessionGraphOptimizationLevel(0);

            using (var session = new InferenceSession(modelPath))
            {
                var inputMeta = session.InputMetadata;
                var container = new List<NamedOnnxValue>();

                foreach (var name in inputMeta.Keys)
                {
                    var tensor = new DenseTensor<float>(image, inputMeta[name].Dimensions);
                    container.Add(NamedOnnxValue.CreateFromTensor<float>(name, tensor));
                }

                foreach (var res in session.Run(container))
                {
                    var maxVal = res.AsTensor<float>().Max();
                    var prob = Softmax(res.AsTensor<float>().ToList<float>());
                    resNumb = res.AsTensor<float>().ToList<float>().IndexOf(maxVal);
                    results.Enqueue(new Tuple<string, int>(imgDir[images.IndexOf(image)], resNumb));
                    var hash = ((IStructuralEquatable)image).GetHashCode(EqualityComparer<float>.Default);
                    using (var db = new ImagesContext())
                    {
                        db.Images.Add(new DataBase.Image()
                        {
                            Name = imgDir[images.IndexOf(image)],
                            Class = resNumb,
                            FileHash = hash,
                            FileContent = ImageToByteArray(imgDir[images.IndexOf(image)])
                        }) ;
                        db.SaveChanges();
                    }
                }
            }
        }

        public static List<double> Softmax (List<float> input)
        {
            var output = new List<double>();
            double max = input.Max();
            for (int i = 0; i < input.Count; i++)
            {
                double elem = Math.Exp(input[i] - max);
                output.Add(elem);
            }
            double sum = output.Sum();
            for (int i = 0; i < input.Count; i++)
            {
                output[i] = output[i]/sum;
            }
            return output;
        }

        public bool DataBaseContains(float[] image, ref int resNumb)
        {
            bool res = false;
            var hash = ((IStructuralEquatable)image).GetHashCode(EqualityComparer<float>.Default);
            using (var db = new ImagesContext())
            {
                var q = from item in db.Images
                        where item.FileHash == hash
                        select item;
                var l = q.ToList();
                foreach (var item in l)
                {
                    item.AccessCount++;
                    if (item.FileContent.Length == image.Length &&
                        item.FileContent.SequenceEqual(ImageToByteArray(imgDir[images.IndexOf(image)])))
                    {
                        res = true;
                        resNumb = item.Class;
                        break;
                    }
                }
            }
            return res;
        }

        public void Stop()
        {
            alive = false;
            autoFinishEvent.Set();
        }

        public byte[] ImageToByteArray(string image)
        {
            Bitmap b = new Bitmap(image);
            using (var stream = new MemoryStream())
            {
                b.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                return stream.ToArray();
            }
        }
    }
}