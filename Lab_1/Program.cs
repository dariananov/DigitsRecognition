using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using ImageRecognizer;

namespace Client
{
    class Program
    {
        static void Main(string[] args)
        {
            bool working = true;
            Stopwatch watch = new Stopwatch();
            int count = 0;

            string imgDir = "/Users/dari/Documents/onnx/testing/";
            string modelPath = "/Users/dari/Documents/onnx/model.onnx";
            Recognizer myRec = new Recognizer(modelPath);
            myRec.InitializePath(imgDir);
            Thread queueThread = new Thread(() =>
            {
                while (working)
                {
                    Tuple<string, int> tmp;
                    if (myRec.results.TryDequeue(out tmp))
                    {
                        Console.WriteLine("File \"" + tmp.Item1 + "\"\thas label " + tmp.Item2 + ".\n");
                        count += 1;
                    }
                    else
                    {
                        Thread.Sleep(2);
                    }
                }
            });

            Thread stopThread = new Thread(() =>
            {
                watch.Start();
                while (watch.ElapsedMilliseconds < 200)
                {
                    Thread.Sleep(2);
                }
                myRec.Stop();
                Console.WriteLine("Stopped");
                working = false;
            });
            
            queueThread.Start();
            stopThread.Start();


            myRec.DoSession();
            working = false;

            
            queueThread.Join();
            stopThread.Join();

            Console.WriteLine();
            Console.WriteLine("Total amount: " + count);
        }
    }
}
