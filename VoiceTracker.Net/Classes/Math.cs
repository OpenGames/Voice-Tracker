using FFTW.NET;
using SharpDX;
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

        private struct f16Complex
        {
            public static f16Complex ImaginaryOne = new f16Complex(0, 1);
            public Half Real { get; }
            public Half Imaginary { get; }
            public float Magnitude { get => (float)Math.Sqrt(Real * Real + Imaginary * Imaginary); }
            public f16Complex(Half real, Half imaginary)
            {
                Real = real;
                Imaginary = imaginary;
                
            }

            public static f16Complex operator *(float left_value, f16Complex right_complex)
            {
                return new f16Complex(right_complex.Real * left_value, right_complex.Imaginary * left_value);
            }
            public static f16Complex operator +(f16Complex left_value, f16Complex right_complex)
            {
                return new f16Complex(right_complex.Real + left_value.Real, right_complex.Imaginary + left_value.Imaginary);
            }

        }

        public class DFT
        {

            public enum DFTModes
            {
                VerySlow,
                FFTW,
                ComplexPreload,
                ComplexPreloadMultithread
            }

            private short[] Values;
            private float[] Amplitudes;
            private float Timespan;
            private float deltaTime;

            private f16Complex[][] PredefinedСomplices;

            private DFTModes DFTMode;
            private float _LoadProgress;
            private string _LoadString;
            private bool _Loaded;


            public bool Busy { get => ApplyForward_Busy; }
            public float LoadProgress { get => _LoadProgress; }
            public string LoadString { get => _LoadString; }
            public bool Loaded { get => _Loaded; }


            public DFT(DFTModes DFTMode, int ValuesCount = -1)
            {
                this.DFTMode = DFTMode;
                if (DFTMode == DFTModes.ComplexPreload)
                {
                    if (ValuesCount <= 0) throw new Exception("ValuesCount cannot be zero or less than zero");

                    //PredefinedСomplices = new List<List<f16Complex>>(ValuesCount);

                    PredefinedСomplices = new f16Complex[ValuesCount / 2][];

                    Task.Factory.StartNew(() =>
                    {
                        _LoadString = "Allocating memory...";
                        for (int k = 0; k < ValuesCount / 2; k++)
                        {
                            PredefinedСomplices[k] = new f16Complex[ValuesCount];
                            //for (int n = 0; n < ValuesCount; n++)
                            //{
                            //    //PredefinedСomplices[k].Add(new f16Complex(0,0));
                            //}

                            _LoadProgress = (k + 1) / (ValuesCount / 200.0f);
                        }

                        _LoadString = "Calculating values...";
                        for (int k = 0; k < ValuesCount / 2; k++)
                        {
                            for (int n = 0; n < ValuesCount; n++)
                            {
                                Complex _tmp = Complex.Exp(-2 * Math.PI * Complex.ImaginaryOne * k * n / ValuesCount);
                                f16Complex _f16_tmp = new f16Complex(new Half((float)_tmp.Real), new Half((float)_tmp.Imaginary));
                                PredefinedСomplices[k][n] = _f16_tmp;
                            }

                            _LoadProgress = (k + 1) / (ValuesCount / 200.0f);
                        }

                        _Loaded = true;

                    });
                }
                _Loaded = true;
            }

            public void SetValues(short[] values, float timespan)
            {
                if (ApplyForward_Busy) return;

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

                switch (this.DFTMode)
                {
                    case DFTModes.VerySlow:
                        ApplyForward_FirstVariant();
                        break;
                    case DFTModes.FFTW:
                        ApplyForward_FFTW();
                        break;
                    case DFTModes.ComplexPreload:
                        ApplyForward_PredefinedExponents();
                        break;
                    case DFTModes.ComplexPreloadMultithread:
                        break;
                    default:
                        break;
                }

                ApplyForward_Busy = false;
            }

            private void ApplyForward_FirstVariant()
            {
                for (int k = 0; k < Values.Length / 2; k++)
                {
                    Complex Amp = 0;
                    var N = Values.Length;
                    for (int n = 0; n < Values.Length; n++)
                    {
                        Amp += Values[n] * Complex.Exp(-2 * Math.PI * Complex.ImaginaryOne * k * n / N);
                    }

                    Amplitudes[k] = (float)Amp.Magnitude / N;
                }
            }

            private void ApplyForward_FFTW()
            {
                Complex[] input = new Complex[Values.Length];
                for (int i = 0; i < Values.Length; i++)
                {
                    input[i] = new Complex(Values[i], 0);
                }
                Complex[] output = new Complex[Values.Length];

                using (var pinIn = new PinnedArray<Complex>(input))
                using (var pinOut = new PinnedArray<Complex>(output))
                {
                    FFTW.NET.DFT.FFT(pinIn, pinOut, PlannerFlags.Estimate, 16);
                }

                for (int k = 0; k < Values.Length / 2; k++)
                {
                    Amplitudes[k] = (float)output[k].Magnitude / Values.Length;
                }
            }

            private void ApplyForward_PredefinedExponents()
            {
                for (int k = 0; k < Values.Length / 2; k++)
                {
                    f16Complex Amp = new f16Complex(0,0);
                    var N = Values.Length;
                    for (int n = 0; n < Values.Length; n++)
                    {
                        Amp += Values[n] * PredefinedСomplices[k][n];
                    }

                    Amplitudes[k] = (float)Amp.Magnitude / N;
                }
            }

            public float Amplitude(float Frequency, float Bandwidth)
            {
                if (Amplitudes == null) return 0;

                //Get k value. F = k / T => k = F*T
                var Kmin = (int)Math.Round((Frequency - Bandwidth) * this.Timespan);
                var Kmax = (int)Math.Round((Frequency + Bandwidth) * this.Timespan);

                if (Kmin < 0) Kmin = 0;
                if (Kmax > Amplitudes.Length) Kmax = Amplitudes.Length;

                decimal Amplitude = 0;

                for (int i = Kmin; i < Kmax + 1; i++)
                {
                    Amplitude = (decimal)Amplitudes[i] > Amplitude ? (decimal)Amplitudes[i] : Amplitude;
                }
                //Amplitude /= (Kmax - Kmin + 1);

                return (float)Amplitude;
            }

            public float MaxAmpFreq()
            {
                if (Amplitudes == null) return 0;
                if (!_Loaded) return 0;

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
