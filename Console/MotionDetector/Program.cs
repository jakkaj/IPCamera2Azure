using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Emgu.CV;
using Emgu.CV.Cvb;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.VideoSurveillance;

namespace MotionDetector
{
    class Program
    {
        private static BackgroundSubtractor _fgDetector;
        private static Emgu.CV.Cvb.CvBlobDetector _blobDetector;
        private static Emgu.CV.Cvb.CvTracks _tracker;

        private static Process _process;

        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Please pass in camera name and address");
                return;
            }

            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;

            _fgDetector = new BackgroundSubtractorMOG2();
            _blobDetector = new CvBlobDetector();
            _tracker = new CvTracks();

            var name = args[0];
            var address = args[1];

            var fn = Path.Combine(Path.GetTempPath(), "survel");

            if (!Directory.Exists(fn))
            {
                Directory.CreateDirectory(fn);
            }
            else
            {
                foreach (var f in Directory.GetFiles(fn))
                {
                   File.Delete(f);
                }
            }

            Task.Run(async () =>
            {
                await _processor(address, fn);
            });

            _watcher(name, fn).GetAwaiter().GetResult();
        }

        private static void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            if (!_process.HasExited)
            {
                _process.Kill();
                Thread.Sleep(2000);
            }

        }

        static Task _watcher(string name, string fn)
        {
            
            var codeFiles = @"C:\Users\jakka\Documents\code.txt";

            var code = File.ReadAllText(codeFiles);

            var func =
                $"https://jordocore.azurewebsites.net/api/VideoUpload?code={code}&SourceName={name}";

            var d = new DirectoryInfo(fn);

            while (true)
            {
                var files = d.GetFiles("*.bmp").OrderBy(_ => _.Name);

                foreach (var f in files)
                {
                    _doDetect(f.FullName);
                    Thread.Sleep(500);
                    f.Delete();
                }
            }

            //var data = File.ReadAllBytes(fn);

            //func.Post(data).Wait();


            //File.Delete(fn);

        }
        static void _doDetect(string fileName)
        {

            Debug.WriteLine($"Processing: {fileName}");

            using (Image<Bgr, Byte> frame = new Image<Bgr, Byte>(fileName))
            {
                
                Mat smoothedFrame = new Mat();
                CvInvoke.GaussianBlur(frame, smoothedFrame, new Size(23, 23), 5); //filter out noises
                //frame._SmoothGaussian(3); 

                
                Mat forgroundMask = new Mat();
                _fgDetector.Apply(smoothedFrame, forgroundMask);
                

                CvBlobs blobs = new CvBlobs();
                _blobDetector.Detect(forgroundMask.ToImage<Gray, byte>(), blobs);
                blobs.FilterByArea(100, int.MaxValue);

                float scale = (frame.Width + frame.Width) / 2.0f;

                File.WriteAllBytes(@"C:\Temp\imagery\aaout.jpg", forgroundMask.ToImage<Gray, byte>().ToJpegData(95));
                

                _tracker.Update(blobs, scale, 5, 5);

                foreach (var pair in _tracker)
                {
                    CvTrack b = pair.Value;
                    CvInvoke.Rectangle(frame, b.BoundingBox, new MCvScalar(255.0, 255.0, 255.0), 2);
                    CvInvoke.PutText(frame, b.Id.ToString(), new Point((int)Math.Round(b.Centroid.X), (int)Math.Round(b.Centroid.Y)), FontFace.HersheyPlain, 1.0, new MCvScalar(255.0, 255.0, 255.0));
                }

                File.WriteAllBytes(@"C:\Temp\imagery\aaframes.jpg", frame.ToJpegData(95));
                File.WriteAllBytes(@"C:\Temp\imagery\aablur.jpg", smoothedFrame.ToImage<Gray, byte>().ToJpegData(95));

                Console.WriteLine($" >>>> Tracker: {_tracker.Count}, Blobs: {blobs.Count}");
            }



            //foreach (var pair in _tracker)
            //{
            //    CvTrack b = pair.Value;
            //    CvInvoke.Rectangle(frame, b.BoundingBox, new MCvScalar(255.0, 255.0, 255.0), 2);
            //    CvInvoke.PutText(frame, b.Id.ToString(), new Point((int)Math.Round(b.Centroid.X), (int)Math.Round(b.Centroid.Y)), FontFace.HersheyPlain, 1.0, new MCvScalar(255.0, 255.0, 255.0));
            //}

            //imageBox1.Image = frame;
            //imageBox2.Image = forgroundMask;
        }

        static async Task _processor(string address, string fn)
        {
            Console.WriteLine("Hello World!");


            while (true)
            {
                var pi = new ProcessStartInfo();

                pi.FileName = @"C:\utils\ffmpeg-20170223-dcd3418-win64-static\ffmpeg-20170223-dcd3418-win64-static\bin\ffmpeg.exe";

                var arguments =
                    $"-y -i {address} -vf drawtext=\"fontfile=C\\\\:/Windows/Fonts/arial.ttf:text='%{{localtime\\:%X}}:fontcolor=yellow'\" -r 1/2 \"{fn}\\out%05d.bmp\"";

                pi.Arguments = arguments;
                pi.WorkingDirectory = Directory.GetCurrentDirectory();
                pi.UseShellExecute = false;

                _process = Process.Start(pi);

                _process.WaitForExit();


            }
        }


    }
}