using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.Generic;

namespace DigitalMusicAnalysis
{
    public class timefreq
    {
        public float[][] timeFreqData;
        public int wSamp;
        private float[][] Y;
        private int N;
        private Complex[] xx;
        //private FFT fft = new FFT();
        private Complex[] twiddles;
        private float fftMax;
        private int size;
        private int blockSize;
        private Stopwatch sw = new Stopwatch();
        public static double timeTakenSTFT;

        public timefreq(float[] x, int windowSamp)
        {
            double pi = 3.14159265;
            Complex i = Complex.ImaginaryOne;
            this.wSamp = windowSamp;
            twiddles = new Complex[wSamp];

            Parallel.For(0, wSamp,
                new ParallelOptions { MaxDegreeOfParallelism = MainWindow.numThreads }, ii =>
                {
                    double a = 2 * pi * ii / (double)wSamp;
                    twiddles[ii] = Complex.Pow(Complex.Exp(-i), (float)a);
                });


            timeFreqData = new float[wSamp / 2][];

            int nearest = (int)Math.Ceiling((double)x.Length / (double)wSamp);
            nearest = nearest * wSamp;

            Complex[] compX = new Complex[nearest];
            for (int kk = 0; kk < nearest; kk++)
            {
                if (kk < x.Length)
                {
                    compX[kk] = x[kk];
                }
                else
                {
                    compX[kk] = Complex.Zero;
                }
            }

            int cols = 2 * nearest / wSamp;

            for (int jj = 0; jj < wSamp / 2; jj++)
            {
                timeFreqData[jj] = new float[cols];
            }

            timeFreqData = stft(compX, wSamp);
        }

        float[][] stft(Complex[] x, int wSamp)
        {
            N = x.Length;
            xx = x;
            fftMax = 0;
            size = 2 * (int)Math.Floor(N / (double)wSamp);
            blockSize = (size - 1 + MainWindow.numThreads - 1) / MainWindow.numThreads;

            Y = new float[wSamp / 2][];

            Parallel.For(0, wSamp / 2, new ParallelOptions { MaxDegreeOfParallelism = MainWindow.numThreads }, ll =>
            {
                Y[ll] = new float[2 * (int)Math.Floor((double)N / (double)wSamp)];
            });

            sw.Start();

            Thread[] fftThreads = new Thread[MainWindow.numThreads];

            for (int i = 0; i < MainWindow.numThreads; i++)
            {
                int id = i;
                fftThreads[i] = new Thread(freqSTFT);
                fftThreads[i].Start(i);
            }
            for (int j = 0; j < MainWindow.numThreads; j++)
            {
                fftThreads[j].Join();
            }

            sw.Stop();
            timeTakenSTFT = sw.Elapsed.TotalMilliseconds;

            for (int ii = 0; ii < 2 * Math.Floor((double)N / (double)wSamp) - 1; ii++)
            {
                for (int kk = 0; kk < wSamp / 2; kk++)
                {
                    Y[kk][ii] /= fftMax;
                }
            }

            return Y;
        }

        public void freqSTFT(object threadID)
        {
            int id = (int)threadID;
            int start = id * blockSize;
            int end = Math.Min(start + blockSize, size - 1);
            // dependencies
            Complex[] temp = new Complex[wSamp];
            Complex[] tempFFT = new Complex[wSamp];

            for (int ii = start; ii < end; ii++)
            {
                for (int jj = 0; jj < wSamp; jj++)
                {
                    temp[jj] = xx[ii * (wSamp / 2) + jj];
                }

                tempFFT = FFT.IterativeFFT(temp, wSamp, twiddles);

                for (int kk = 0; kk < wSamp / 2; kk++)
                {
                    Y[kk][ii] = (float)Complex.Abs(tempFFT[kk]);

                    if (Y[kk][ii] > fftMax)
                    {
                        fftMax = Y[kk][ii];
                    }
                }
            }
        }
    }
}
