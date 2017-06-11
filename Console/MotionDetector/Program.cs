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
using Emgu.CV.Util;
using Emgu.CV.VideoSurveillance;
using ExifLib;
using ExtensionGoo.Standard.Extensions;

namespace MotionDetector
{
    class Program
    {
        private static BackgroundSubtractor _fgDetector;
        private static Emgu.CV.Cvb.CvBlobDetector _blobDetector;
        private static Emgu.CV.Cvb.CvTracks _tracker;

        private static Process _process;

        private static DateTime? _lastMovement;
        private static DateTime _currentDate;

        const string ff = @"C:\utils\ffmpeg-20170223-dcd3418-win64-static\ffmpeg-20170223-dcd3418-win64-static\bin\ffmpeg.exe";
        private static string _code = null;
        private static string _name = null;
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Please pass in camera name and address");
                return;
            }

            var codeFiles = @"C:\Users\jakka\Documents\code.txt";

            _code = File.ReadAllText(codeFiles);


            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
            
            _fgDetector = new BackgroundSubtractorMOG2();
            _blobDetector = new CvBlobDetector();
            _tracker = new CvTracks();

            _name = args[0];
            var address = args[1];

            var fn = Path.Combine(Path.GetTempPath(), "survel");

            if (!Directory.Exists(fn))
            {
                Directory.CreateDirectory(fn);
            }
            else
            {
                //foreach (var f in Directory.GetFiles(fn))
                //{
                //   File.Delete(f);
                //}
            }

            Task.Run(async () =>
            {
                await _processor(address, fn);
            });

            _watcher(_name, fn).GetAwaiter().GetResult();
        }

        private static void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            if (!_process.HasExited)
            {
                _process.Kill();
                Thread.Sleep(2000);
            }

        }

        static async Task _watcher(string name, string fn)
        {
            
            

            var d = new DirectoryInfo(fn);

            DateTime? triggeredDate = null;

            while (true)
            {
                
                var files = d.GetFiles("*.jpg").OrderBy(_ => _.Name);

                foreach (var f in files)
                {
                    var datePictureTaken = File.GetCreationTime(f.FullName);
                    _currentDate = datePictureTaken;

                    if (_doDetect(f.FullName))
                    {
                        if (triggeredDate == null)
                        {
                            triggeredDate = _currentDate;
                        }

                        _lastMovement = _currentDate;
                        
                        using (var imgFull = new Image<Bgr, Byte>(f.FullName))
                        {
                            var upl = imgFull.ToJpegData(65);

                            var func =
                                $"https://jordocore.azurewebsites.net/api/MovementUploader?code={_code}&SourceName={name}&Ext=jpg&Ticks={datePictureTaken.Ticks}";

                            await func.Post(upl);
                            Console.WriteLine($">>>> Sent {DateTime.Now.ToString()}");
                        }
                    }

                    var _movementWindow = _lastMovement != null ?
                        _currentDate.Subtract(_lastMovement.Value) : TimeSpan.Zero;

                    if (triggeredDate.HasValue)
                    {
                        Debug.WriteLine($"Movement was {_movementWindow.TotalSeconds} ago");
                    }

                    if (triggeredDate.HasValue && _movementWindow > TimeSpan.FromSeconds(5))
                    {
                        //movement is now old, collect and transmit the movement.  
                        await _buildAndTransmit(triggeredDate.Value);
                        triggeredDate = null;
                    }


                    _doScollingTimeWindow(f);
                    
                    f.Delete();
                }

                await Task.Delay(5000);
            }

            //var data = File.ReadAllBytes(fn);

            //func.Post(data).Wait();


            //File.Delete(fn);

        }

        static async Task _buildAndTransmit(DateTime eventStart)
        {
            var timeWindowDirecgtory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), "videoWindow"));
            var stageDirectory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), "videoWindowStage"));

            if (!stageDirectory.Exists)
            {
                stageDirectory.Create();
            }

            var count = 1;

            foreach (var f in timeWindowDirecgtory.GetFiles())
            {
                if (f.CreationTime < eventStart.Subtract(TimeSpan.FromSeconds(3)))
                {
                    continue;
                }

                var target = Path.Combine(stageDirectory.FullName,
                    $"img{count:D5}.jpg");
               
                f.CopyTo(target, true);
                count++;
            }

            var pi = new ProcessStartInfo();

            pi.FileName = ff;

            var arguments =
                $"-y -framerate 1/1 -i img%05d.jpg -c:v libx264 -vf \"fps=5,format=yuv420p\" video.mp4";

            pi.Arguments = arguments;
            pi.WorkingDirectory = stageDirectory.FullName;
            pi.UseShellExecute = true;

            _process = Process.Start(pi);

            _process.WaitForExit();

            foreach (var fDelete in stageDirectory.GetFiles("*.jpg"))
            {
                fDelete.Delete();
            }

            var fVideo = Path.Combine(stageDirectory.FullName, "video.mp4");

            if (!File.Exists(fVideo))
            {
                return;
            }

            var d = File.ReadAllBytes(fVideo);

            var func =
                $"https://jordocore.azurewebsites.net/api/MovementUploader?code={_code}&SourceName={_name}&Ext=mp4&Ticks={_currentDate.Ticks}";

            Debug.WriteLine($">>> Transmit {func}");

            await func.Post(d);

            
        }

        static void _doScollingTimeWindow(FileInfo file)
        {
            var datePictureTaken = file.CreationTime;
            var ticks = datePictureTaken.Ticks;
            var ext = file.Extension;

            var timeWindow = Path.Combine(Path.GetTempPath(), "videoWindow", $"{ticks}{ext}");

            var newFile = new FileInfo(timeWindow);

            if (!newFile.Directory.Exists)
            {
                newFile.Directory.Create();
            }

            file.CopyTo(newFile.FullName, true);

            if (!_isMovementEventActive())
            {
                _trimOld(newFile.Directory);
            }
        }

        static bool _isMovementEventActive()
        {
            if (!_lastMovement.HasValue)
            {
                return false;
            }

            return _currentDate.Subtract(_lastMovement.Value) < TimeSpan.FromSeconds(30);
        }

        static void _trimOld(DirectoryInfo dir)
        {
            var fs = dir.GetFiles();
            var dtNow = _currentDate;

            

            foreach (var file in fs)
            {
                var fn = file.Name.Replace(file.Extension, "");
                var dt = new DateTime(Convert.ToInt64(fn));
                if (dtNow.Subtract(dt) > TimeSpan.FromSeconds(30))
                {
                    file.Delete();
                }
            }
        }

        static private Image<Gray, Byte> _original = null;

        static bool _doDetect(string fileName)
        {
            
            Debug.WriteLine($"Processing: {fileName}");

            using (Image<Bgr, Byte> frameOrig = new Image<Bgr, Byte>(fileName))
            {
                var frame = frameOrig.Convert<Gray, Byte>();

                Mat smoothedFrame = new Mat();
                CvInvoke.GaussianBlur(frame, smoothedFrame, new Size(19, 19), 7); //filter out noises

                var smoothedImage = smoothedFrame.ToImage<Gray, Byte>();

                if (_original == null)
                {
                    _original = smoothedImage;
                    return false;
                }

                var frameDelta = smoothedImage.AbsDiff(_original);
                var thresh = frameDelta.ThresholdBinary(new Gray(25), new Gray(255));
                thresh = thresh.Dilate(2);

                //File.WriteAllBytes(@"C:\Temp\imagery\aathreh.jpg", thresh.ToJpegData(95));

                _original = smoothedImage;
                
                //var cnts = new VectorOfVectorOfPoint();
                //CvInvoke.FindContours(thresh.Copy(), cnts, null, RetrType.External,
                //    ChainApproxMethod.ChainApproxSimple);

                //var goer = false;

                //for (var i = 0; i < cnts.Size; i++)
                //{
                //    var c = cnts[i];

                //    if (CvInvoke.ContourArea(c) < 500)
                //    {
                //        continue;
                //    }
                //    goer = true;


                //    //Debug.WriteLine(CvInvoke.ContourArea(c));
                //    //var rect = CvInvoke.BoundingRectangle(c);
                //    //CvInvoke.Rectangle(frame, rect, new MCvScalar(255.0, 255.0, 255.0), 2);
                //}

                ////// File.WriteAllBytes(@"C:\Temp\imagery\aaframes.jpg", frame.ToJpegData(95));

                // return goer;

                //Mat forgroundMask = new Mat();
                //_fgDetector.Apply(smoothedFrame, forgroundMask);

                CvBlobs blobs = new CvBlobs();
                _blobDetector.Detect(thresh, blobs);
                blobs.FilterByArea(800, int.MaxValue);

                float scale = (frame.Width + frame.Width) / 2.0f;

                //File.WriteAllBytes(@"C:\Temp\imagery\aaout.jpg", smoothedImage.ToJpegData(95));


                _tracker.Update(blobs, scale, 5, 5);

                foreach (var pair in _tracker)
                {
                    CvTrack b = pair.Value;
                    CvInvoke.Rectangle(frame, b.BoundingBox, new MCvScalar(255.0, 255.0, 255.0), 2);
                    CvInvoke.PutText(frame, b.Id.ToString(), new Point((int)Math.Round(b.Centroid.X), (int)Math.Round(b.Centroid.Y)), FontFace.HersheyPlain, 1.0, new MCvScalar(255.0, 255.0, 255.0));
                }

             //  File.WriteAllBytes(@"C:\Temp\imagery\aaframes.jpg", frame.ToJpegData(95));
               // File.WriteAllBytes(@"C:\Temp\imagery\aablur.jpg", smoothedFrame.ToImage<Gray, byte>().ToJpegData(95));

                return _tracker.Count > 0;
                //var cnts = new VectorOfVectorOfPoint();
                //CvInvoke.FindContours(thresh.Copy(), cnts, null, RetrType.External,
                //    ChainApproxMethod.ChainApproxSimple);



                //for (var i = 0; i < cnts.Size; i++)
                //{
                //    var c = cnts[i];
                //    Debug.WriteLine(CvInvoke.ContourArea(c));
                //    var rect = CvInvoke.BoundingRectangle(c);
                //    CvInvoke.Rectangle(frame, b.BoundingBox, new MCvScalar(255.0, 255.0, 255.0), 2);


                //}





                //Mat smoothedFrame = new Mat();
                //CvInvoke.GaussianBlur(frame, smoothedFrame, new Size(23, 23), 5); //filter out noises
                ////frame._SmoothGaussian(3); 


                //Mat forgroundMask = new Mat();
                //_fgDetector.Apply(smoothedFrame, forgroundMask);


                //CvBlobs blobs = new CvBlobs();
                //_blobDetector.Detect(forgroundMask.ToImage<Gray, byte>(), blobs);
                //blobs.FilterByArea(100, int.MaxValue);

                //float scale = (frame.Width + frame.Width) / 2.0f;

                //File.WriteAllBytes(@"C:\Temp\imagery\aaout.jpg", forgroundMask.ToImage<Gray, byte>().ToJpegData(95));


                //_tracker.Update(blobs, scale, 5, 5);

                //foreach (var pair in _tracker)
                //{
                //    CvTrack b = pair.Value;
                //    CvInvoke.Rectangle(frame, b.BoundingBox, new MCvScalar(255.0, 255.0, 255.0), 2);
                //    CvInvoke.PutText(frame, b.Id.ToString(), new Point((int)Math.Round(b.Centroid.X), (int)Math.Round(b.Centroid.Y)), FontFace.HersheyPlain, 1.0, new MCvScalar(255.0, 255.0, 255.0));
                //}

                //File.WriteAllBytes(@"C:\Temp\imagery\aaframes.jpg", frame.ToJpegData(95));
                //File.WriteAllBytes(@"C:\Temp\imagery\aablur.jpg", smoothedFrame.ToImage<Gray, byte>().ToJpegData(95));

                //Console.WriteLine($" >>>> Tracker: {_tracker.Count}, Blobs: {blobs.Count}");
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

            foreach (var process in Process.GetProcessesByName("ffmpeg"))
            {
                process.Kill();
            }


            while (true)
            {
                var pi = new ProcessStartInfo();

                pi.FileName = ff;

                var arguments =
                    $"-y -i {address} -vf drawtext=\"fontfile=C\\\\:/Windows/Fonts/arial.ttf:text='%{{localtime\\:%X}}:fontcolor=yellow'\" -r 1/1 \"{fn}\\out%05d.jpg\"";

                pi.Arguments = arguments;
                pi.WorkingDirectory = Directory.GetCurrentDirectory();
                pi.UseShellExecute = true;
              
                
                _process = Process.Start(pi);

                _process.WaitForExit();


            }
        }


    }
}