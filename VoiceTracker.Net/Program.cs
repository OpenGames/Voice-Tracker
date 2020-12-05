using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using SharpDX;
using SharpDX.DirectSound;
using SharpDX.Mathematics.Interop;
using SharpDX.Multimedia;


using VoiceTracker.Net.Classes;

namespace VoiceTracker.Net
{
    class Program
    {
        static DirectSoundCapture directSoundDeviceIn;
        static CaptureBufferDescription captureBufferDescription = new CaptureBufferDescription();
        static WaveFormat waveFormat = new WaveFormat(48000, 16, 1);
        static CaptureBuffer captureBuffer;

        static MyMath.DFT FourierTransformer = new MyMath.DFT();

        static float[] frequencies;

        static bool isWorking = true;

        static void Main(string[] args)
        {
            DeviceInformation device;
            var DevList = DirectSoundCapture.GetDevices();

            for (int i = 0; i < DevList.Count; i++)
            {
                DeviceInformation Device = DevList[i];
                Console.WriteLine($"[{i}] {Device.Description}");
            }

            Console.Write("> ");

            int answer = Convert.ToInt32(Console.ReadLine());
            if (answer < 0 || answer >= DevList.Count) Console.WriteLine("u r stupid");

            int index = answer;
            device = DevList[index];

            Console.WriteLine(device.DriverGuid);

            directSoundDeviceIn = new DirectSoundCapture(device.DriverGuid);
            captureBufferDescription.BufferBytes = 192000; // 2 Second Buffer
            captureBufferDescription.Format = waveFormat;
            //captureBufferDescription.ControlEffects = false;
            //captureBufferDescription.WaveMapped = true;

            captureBuffer = new CaptureBuffer(directSoundDeviceIn, captureBufferDescription);

            frequencies = new float[20];
            for (int i = 0; i < frequencies.Length; i++)
            {
                frequencies[i] = MyMath.DFT.FrequencyFromIndex(index, frequencies.Length, waveFormat.SampleRate / 2);
            }

            Thread listenThread = new Thread(new ThreadStart(Listen)) { Name = "ListenThread", IsBackground = true };
            Thread infoThread = new Thread(new ThreadStart(DrawBufferInfo)) { Name = "DrawBufferInfoThread", IsBackground = true };
            Thread KeyboardListenThread = new Thread(new ThreadStart(KeyboardListen)) { Name = "KeyboardListen", IsBackground = true };

            Console.Clear();

            listenThread.Start();
            KeyboardListenThread.Start();
            infoThread.Start();

            listenThread.Join();
            KeyboardListenThread.Join();
            infoThread.Join();
        }

        static bool KeyboardListen_Drawing = false;
        static string KeyboardListen_CommandBuffer = "";
        static string KeyboardListen_CommandAnswerBuffer = "";
        static void KeyboardListen()
        {
            while(isWorking)
            {
                var Key = Console.ReadKey();
                KeyboardListen_Drawing = true;

                KeyboardListen_CommandBuffer += Key.KeyChar;

                if (Key.Key == ConsoleKey.Enter)
                {
                    //KeyboardListen_CommandBuffer = "";
                    KeyboardListen_CommandAnswerBuffer = "";
                    HandleCommand();
                }

                

                int pLeft = Console.CursorLeft;
                int pTop = Console.CursorTop;

                Console.SetCursorPosition(0, Console.WindowHeight - 3);
                Console.Write($"Tx: > {KeyboardListen_CommandBuffer.PadRight(Console.WindowWidth - 6)}");
                Console.Write($"Rx: > {KeyboardListen_CommandAnswerBuffer.PadRight(Console.WindowWidth - 6)}");
                Console.SetCursorPosition(pLeft, pTop);

                KeyboardListen_Drawing = false;
            }
        }
        static void HandleCommand()
        {
            if(KeyboardListen_CommandBuffer == "/dump\r")
            {
                byte[] bytes = new byte[_Buffer.Length * 2];
                Buffer.BlockCopy(_Buffer, 0, bytes, 0, _Buffer.Length * 2);
                File.WriteAllBytes("buffer.bin", bytes);

                string text = "";
                foreach (var num in _Buffer)
                {
                    text += $"{num},";
                }
                File.WriteAllText("buffer.txt", text);

                KeyboardListen_CommandAnswerBuffer = "Dumped to buffer.bin and buffer.txt";
                KeyboardListen_CommandBuffer = "";
            }
            else
            {
                KeyboardListen_CommandAnswerBuffer = "No such command, sorry";
                KeyboardListen_CommandBuffer = "";
            }
        }

        const int BufferSize = 48000 * 2;
        static long BufferCursor = 0;
        static short[] _Buffer = new short[BufferSize];
        static bool stopFlag = false;

        static DataStream kek2;
        static DataStream kek;

        static void Listen()
        {
            while (isWorking)
            {
                if (captureBuffer.Capturing)
                {

                    if((!BufferAverage_Calculating && !BufferAmp_Calculating && !LastNValues_Busy) && !stopFlag)
                    {
                        int pos = captureBuffer.CurrentRealPosition;

                        int startIndex = (pos > BufferSize) ? pos - BufferSize : 0;
                        int count = (pos > BufferSize) ? ((pos + BufferSize > captureBufferDescription.BufferBytes) ? (captureBufferDescription.BufferBytes - pos) : BufferSize) : pos;

                        if (count == 0) continue;

                        kek = captureBuffer.Lock(startIndex, count, LockFlags.None, out kek2);

                        byte[] _tmpBuf = new byte[count];
                        kek.Read(_tmpBuf, 0, count);
                        Buffer.BlockCopy(_tmpBuf, 0, _Buffer, (int)(BufferCursor % BufferSize), count);
                        BufferCursor += count / 2;

                        FourierTransformer.SetValues(LastNValues(ref _Buffer, 48000), 1.0f);

                        captureBuffer.Unlock(kek, kek2);

                        //stopFlag = true;

                        //captureBuffer.Read(_Buffer, 0, count, startIndex, LockFlags.None);
                    }

                    dBytes = captureBuffer.CurrentCapturePosition - pCurrentCapturePosition;
                    if (dBytes < 0) dBytes = dBytes + 192000;
                    pCurrentCapturePosition = captureBuffer.CurrentCapturePosition;

                    SecondsCaptured += 2 * (float)dBytes / captureBufferDescription.BufferBytes;
                }
                else
                {
                    captureBuffer.Start(new RawBool());
                }
                Thread.Sleep(5);
            }
        }

        static int ReadCounter = 0;
        static int dBytes = 0;
        static int pCurrentCapturePosition = 0;
        static float SecondsCaptured = 0;

        static void DrawBufferInfo()
        {
            int c = 0;
            while (isWorking)
            {
                if (KeyboardListen_Drawing) continue;

                if (c++ > 100) { c = 0; Console.Clear(); }

                if (captureBuffer.Capturing)
                {
                    Console.SetCursorPosition(0, 0);
                    string text = $"[  ReadCounter  ]: {ReadCounter++                       }    \n" +
                                  $"[SecondsCaptured]: {SecondsCaptured                     }    \n" +
                                  $"[     dBytes    ]: {dBytes                              }    \n" +
                                  $"[   CaptCurPos  ]: {captureBuffer.CurrentCapturePosition}    \n" +
                                  $"[   ReadCurPos  ]: {captureBuffer.CurrentRealPosition   }    \n" +
                                  $"[  BufferCursor ]: {BufferCursor}       \n" +
                                  $"[    kek2stat   ]: {kek2}               \n" +
                                  $"[    kek stat   ]: {kek}                \n" +
                                  $"\n" +
                                  $"\n" +
                                  $"\n" +
                                  $"[ Average Value ]: {BufferAverage(ref _Buffer)}    \n" +
                                  $"[   Amplitude   ]: {BufferAmp(ref _Buffer)}    \n" + 
                                  $"[   Main Freq   ]: {FourierTransformer.MaxAmpFreq()}    \n";

                    Console.Write(text);

                    Console.SetCursorPosition(0, 15);
                    string[] textTable = new string[11];
                    for (int i = 0; i < 11; i++)
                    {
                        textTable[i] = ("".PadLeft(25));
                    }

                    FourierTransformer.ApplyForward();

                    //float[] Amps = new float[frequencies.Length];
                    //for (int i = 0; i < frequencies.Length; i++)
                    //{
                    //    Amps[i] = FourierTransformer.Amplitude(frequencies[i]);

                    //    int Height = (int)(Amps[i] / 65535);
                    //    for (int j = 0; j < Height; j++)
                    //    {
                    //        textTable[Height - j] = new StringBuilder(textTable[Height - j]) { [i + 5] = '#' }.ToString();
                    //    }
                        
                    //}

                    //text = string.Join("\n", textTable);
                    //Console.Write(text);
                }
                Thread.Sleep(100);
            }
        }

        static bool BufferAverage_Calculating;
        static double BufferAverage(ref short[] Buffer)
        {
            BufferAverage_Calculating = true;

            double result = 0;

            for (int i = 0; i < Buffer.Length; i++)
            {
                result += Buffer[i];
            }
            result /= Buffer.Length;

            BufferAverage_Calculating = false;
            return result;
        }

        static bool BufferAmp_Calculating;
        static short BufferAmp(ref short[] Buffer)
        {
            BufferAmp_Calculating = true;

            short result = (short)(Buffer.Max() - Buffer.Min());

            BufferAmp_Calculating = false;
            return result;
        }

        static bool LastNValues_Busy;
        static short[] LastNValues(ref short[] refBuffer, int count)
        {
            LastNValues_Busy = true;
            short[] result = new short[count];

            if((BufferCursor % BufferSize) >= count)
            {
                Array.Copy(refBuffer, (BufferCursor % BufferSize) - count, result, 0, count);
            }
            else
            {
                Array.Copy(refBuffer, BufferSize - (count - (BufferCursor % BufferSize)), result, 0, count - (BufferCursor % BufferSize) - 1);
                Array.Copy(refBuffer, 0, result, count - (BufferCursor % BufferSize), (BufferCursor % BufferSize));
            }
            LastNValues_Busy = false;
            return result;

        }
    }
}
