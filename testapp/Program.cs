using System;
using System.IO;
using System.Drawing;
using FFMediaToolkit.Decoding;
using FFMediaToolkit.Graphics;
using FFMediaToolkit;
using System.Diagnostics;
using SixLabors.ImageSharp.Processing.Processors.Transforms;
using NAudio.Wave;
using FFmpeg.AutoGen;
using System.Security.AccessControl;
using System.Text;
using System.Runtime.InteropServices;
using NAudio.Wave.SampleProviders;

namespace VideoPixelChangeDetector
{
    class Program
    {
        static string filePath = @"C:\temp\test.txt"; // A test file path
        static string text = "Hello world!"; // A test string to append

        static async Task Main(string[] args)
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            string videoDirectoryPath = @"C:\Users\finepointcgi\Documents\silence-speedup\tmp\";
            double thresholdPercentage = 6.0; // Configurable percentage threshold
            
            Stopwatch timer = new Stopwatch(); 
            string[] videoFiles = Directory.GetFiles(videoDirectoryPath, "*.mkv", SearchOption.TopDirectoryOnly);
            FFmpegLoader.FFmpegPath = @"C:\Users\finepointcgi\source\repos\testapp\testapp\";
            int i = 0;
            List<Task> tasks = new List<Task>();
            timer.Start();
            List<string> filesToBeRemoved = new List<string>();
            foreach (var videoFile in videoFiles)
            {
                tasks.Add(Task.Run(() =>
                {
                    if (!IsPixelChangeAboveThreshold(videoFile, thresholdPercentage))
                    {
                        filesToBeRemoved.Add(videoFile);
                        Console.WriteLine("remove file: " + videoFile);
                    }
                    else
                    {
                        //Console.WriteLine("file is safe " + videoFile);
                    }
                }));
            }
            await Task.WhenAll(tasks);
            timer.Stop();
            foreach (var item in filesToBeRemoved)
            {
                File.Delete(item);
            }
            Console.WriteLine(timer.Elapsed.ToString());
        }

        static bool IsPixelChangeAboveThreshold(string videoFilePath, double thresholdPercentage)
        {
            if (DetectSoundInVideo(videoFilePath, 0.01f) > 0) //IsMaxVolumeAboveThreshold(videoFilePath, 0.001f))
            {
                //Console.WriteLine("files has audio stream returning");
                return true;
            }
            using var videoFile = MediaFile.Open(videoFilePath);
            var videoStream = videoFile.Video;
            var totalFrames = videoStream.Info.NumberOfFrames;
            Image<Rgba32> previousFrame = null;
            double totalPixelDifference = 0;
            List<Image<Rgba32>> images = new List<Image<Rgba32>>();
            MediaFile file = MediaFile.Open(videoFilePath);
            int targetWidth = 1280;
            int targetHeight = 720;
            
            while (file.Video.TryGetNextFrame(out ImageData imageData))
            {
                if (previousFrame == null)
                {
                    previousFrame = Image.LoadPixelData<Rgba32>(imageData.Data, imageData.ImageSize.Width, imageData.ImageSize.Height);
                }
                //images.Add(Image.LoadPixelData<Rgba32>(imageData.Data, imageData.ImageSize.Width, imageData.ImageSize.Height));
                Image<Rgba32> currentFrame = Image.LoadPixelData<Rgba32>(imageData.Data, imageData.ImageSize.Width, imageData.ImageSize.Height);

                double framePixelDifference = CalculatePixelDifference(previousFrame, currentFrame);
                totalPixelDifference += framePixelDifference;

                previousFrame = currentFrame;

            }

            thresholdPercentage = thresholdPercentage * (previousFrame.Width * previousFrame.Height) / (targetHeight * targetWidth);

            int frameWidth = previousFrame.Width;
            int frameHeight = previousFrame.Height;
            double averagePixelDifference = (double)(totalPixelDifference / (totalFrames - 1));
            double averagePixelDifferencePercentage = (averagePixelDifference / ((1920) * (1080))) ;
            //File.AppendAllText(filePath, "\n" + videoFilePath + " Pixel Dif Precentage " + averagePixelDifferencePercentage);
            Console.WriteLine(videoFilePath + " Pixel Dif Precentage " + averagePixelDifferencePercentage);
            return averagePixelDifferencePercentage >= thresholdPercentage;
        }

        static int DetectSoundInVideo(string videoFilePath, float soundThreshold)
        {
            int soundFrames = 0; // A variable to store the number of sound frames
            using (var reader = new MediaFoundationReader(videoFilePath)) // A reader to access the audio stream of the video file
            {
                var sampleChannel = new SampleChannel(reader); // A channel to read the samples from the reader
                var buffer = new float[reader.WaveFormat.SampleRate * reader.WaveFormat.Channels]; // A buffer to store the samples
                int samplesRead; // A variable to store the number of samples read
                while ((samplesRead = sampleChannel.Read(buffer, 0, buffer.Length)) > 0) // A loop to read the samples until the end of the stream
                {
                    float maxSample = 0f; // A variable to store the maximum sample value in the buffer
                    for (int i = 0; i < samplesRead; i++) // A loop to iterate over the samples in the buffer
                    {
                        maxSample = Math.Max(maxSample, Math.Abs(buffer[i])); // Update the maximum sample value with the absolute value of the current sample
                    }
                    if (maxSample > soundThreshold) // If the maximum sample value is above the sound threshold
                    {
                        soundFrames++; // Increment the number of sound frames
                    }
                }
            }
            //Console.WriteLine(videoFilePath + " sound frames " + soundFrames);
            return soundFrames; // Return the number of sound frames
        }

        static bool IsMaxVolumeAboveThreshold(string videoFilePath, float volumeThreshold)
        {
            bool result = false;
            using var reader = new MediaFoundationReader(videoFilePath);
            var sampleChannel = new SampleChannel(reader);
            var meter = new MeteringSampleProvider(sampleChannel);

            meter.StreamVolume += (s, e) =>
            {
                //Console.WriteLine(videoFilePath +" Volume " + e.MaxSampleValues[0]);
                if (e.MaxSampleValues[0] > volumeThreshold)
                {
                    result = true;
                }
            };

            var buffer = new float[reader.WaveFormat.SampleRate * reader.WaveFormat.Channels];
            while (meter.Read(buffer, 0, buffer.Length) > 0) { }
            return result;
        }

        static double CalculatePixelDifference(Image<Rgba32> frame1, Image<Rgba32> frame2)
        {
            double difference = 0;
            
            //frame1.Mutate(ctx => ctx.Resize(new ResizeOptions {
            //    Size = new SixLabors.ImageSharp.Size(1280, 720),
            //    Sampler = new NearestNeighborResampler()
            //})); 
            //frame2.Mutate(ctx => ctx.Resize(new ResizeOptions
            //{
            //    Size = new SixLabors.ImageSharp.Size(1280, 720),
            //    Sampler = new NearestNeighborResampler()
            //}));

            int width = frame1.Width;
            int height = frame1.Height;
            

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                  
                    Rgba32 pixel1 = frame1[x, y];
                    Rgba32 pixel2 = frame2[x, y];

                    difference += Math.Abs(pixel1.R - pixel2.R);
                    difference += Math.Abs(pixel1.G - pixel2.G);
                    difference += Math.Abs(pixel1.B - pixel2.B);
                }
            }

            if (difference > 0)
            {
               // difference = difference / (width * height * 3);
            }

            return difference;
        }

    }
}