using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using ExtensionGoo.Standard.Extensions;

namespace StreamRipper
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Please pass in camera name and address");
                return;
            }

            var name = args[0];
            var address = args[1];

            Console.WriteLine("Hello World!");

            var codeFiles = @"C:\Users\jak\Documents\code.txt";

            var code = File.ReadAllText(codeFiles);

            var func =
                $"https://jordocore.azurewebsites.net/api/VideoUpload?code={code}&SourceName={name}";

            while (true)
            {
                var pi = new ProcessStartInfo();

                pi.FileName = @"E:\Tools\ffmpeg\ffmpeg-20170112-6596b34-win64-static\bin\ffmpeg.exe";

                var fn = Path.GetTempFileName();

                var arguments =
                    $"-y -i {address}  -t 15  -r 3 -vf drawtext=\"fontfile=C\\\\:/Windows/Fonts/arial.ttf:text='%{{localtime\\:%X}}:fontcolor=yellow'\" -c:v libx264 -b 50000 -pix_fmt yuv420p -f mp4 \"{fn}\"";

                pi.Arguments = arguments;
                pi.WorkingDirectory = Directory.GetCurrentDirectory();
                pi.UseShellExecute = false;
                var process = Process.Start(pi);

                process.WaitForExit((int)TimeSpan.FromSeconds(320).TotalMilliseconds);

                if (!process.HasExited)
                {
                    process.Kill();
                    Thread.Sleep(2000);
                }

                if (!File.Exists(fn))
                {
                    Thread.Sleep(5000);
                    continue;
                }

                Thread.Sleep(1000);

                var data = File.ReadAllBytes(fn);

                func.Post(data).Wait();

                File.Delete(fn);
            }
        }
    }
}