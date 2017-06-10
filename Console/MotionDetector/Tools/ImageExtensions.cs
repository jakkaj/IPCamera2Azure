using System.IO;
using Emgu.CV;
using Emgu.CV.Structure;

namespace MotionDetector.Tools
{
    public static class ImageExtensions
    {
       

        public static Image<Gray, byte> ToGreyImage(this byte[] bytes)
        {
            //sigh - there must be a better way. But we can't use the image constructor to do it becasue we don't know the dimensions of the image here
            var f = Path.GetTempFileName();
            File.WriteAllBytes(f, bytes);

            var img = new Image<Gray, byte>(f);

            File.Delete(f);

            return img;
        }

        public static Image<Bgr, byte> ToBgrImage(this byte[] bytes)
        {
            //sigh - there must be a better way. But we can't use the image constructor to do it becasue we don't know the dimensions of the image here
            var f = Path.GetTempFileName();
            File.WriteAllBytes(f, bytes);

            var img = new Image<Bgr, byte>(f);

            File.Delete(f);

            return img;
        }
    }
}
