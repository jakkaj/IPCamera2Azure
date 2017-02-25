using System;
using System.Diagnostics;
using System.IO;
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
                var fn = Path.Combine(Directory.GetCurrentDirectory(), DateTime.UtcNow.Ticks + ".mp4");

                var arguments =
                    $"-y -i {address} -t 60 -r 3 -c:v libx264 -b 50000 -pix_fmt yuv420p -f mp4 \"{fn}\"";;

                // var arguments =
                //"-y -i http://admin@10.0.0.140:99/videostream.asf?user=admin&pwd=&resolution=64 -t 15 -r 3 -c:v libx264 -b 50000 -pix_fmt yuv420p -f mp4 \"" +
                //fn + "\"";

                pi.Arguments = arguments;
                pi.WorkingDirectory = Directory.GetCurrentDirectory();
                pi.UseShellExecute = false;
                var process = Process.Start(pi);

                process.WaitForExit();

                var data = File.ReadAllBytes(fn);

                func.Post(data).Wait();

                File.Delete(fn);
            }
        }
    }
}