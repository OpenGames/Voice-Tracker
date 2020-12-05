using FFTW.NET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace VoiceTracker.Net.Classes
{
    class MyMath
    {
        public static void A()
        {
        }

        public class DFT
        {

            private short[] Values;
            private float[] Amplitudes;
            private float Timespan;
            private float deltaTime;

            public bool Busy { get => ApplyForward_Busy; }

            public void SetValues(short[] values, float timespan)
            {
                Timespan = timespan;
                deltaTime = timespan / values.Length;

                Values = values;
            }

            private bool ApplyForward_Busy = false;
            public void ApplyForward()
            {
                if (Values == null || Values.Length == 0) return;

                ApplyForward_Busy = true;

                Amplitudes = new float[Values.Length / 2];

                Complex[] input = new Complex[Values.Length];
                for (int i = 0; i < Values.Length; i++)
                {
                    input[i] = new Complex(Values[i], 0);
                }
                Complex[] output = new Complex[Values.Length];

                using (var pinIn = new PinnedArray<Complex>(input))
                using (var pinOut = new PinnedArray<Complex>(output))
                {
                    FFTW.NET.DFT.FFT(pinIn, pinOut);
                }

                for (int k = 0; k < Values.Length / 2; k++)
                {
                    Amplitudes[k] = (float)output[k].Magnitude / Values.Length;
                }

                //

                //for (int k = 0; k < Values.Length / 2; k++)
                //{
                //    Complex Amp = 0;
                //    var N = Values.Length;
                //    for (int n = 0; n < Values.Length; n++)
                //    {
                //        Amp += Values[n] * Complex.Exp(-2 * Math.PI * Complex.ImaginaryOne * k * n / N);
                //    }

                //    Amplitudes[k] = (float)Amp.Magnitude / N;
                //}

                ApplyForward_Busy = false;
            }

            public float Amplitude(float Frequency)
            {
                if (Amplitudes == null) return 0;

                //Get k value. F = k / T => k = F*T
                var K = (int)Math.Round(Frequency * this.Timespan);
                var Amplitude = Amplitudes[K];

                return Amplitude;
            }

            public float MaxAmpFreq()
            {
                if (Amplitudes == null) return 0;

                return Array.IndexOf(Amplitudes, Amplitudes.Max());
            }

            public static float FrequencyFromIndex(int index, int maxIndex, float maxFrequency)
            {
                return maxFrequency * (float)Math.Log((float)index / maxIndex + 1);
            }

            public void Clear()
            {
                Values = null;
                Amplitudes = null;
                Timespan = 0;
            }

        }

    }
}
