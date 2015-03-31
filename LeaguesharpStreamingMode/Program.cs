using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.IO;
using System.Threading;

namespace LeaguesharpStreamingMode
{
    class Program
    {
        
        static void Main(string[] args)
        {
            new LeaguesharpStreamingMode.PatchLs().DoWork();
            Console.ReadLine();
        }

    }

    public class PatchLs
    {

        private static readonly Assembly _lib = Assembly.Load(LeaguesharpStreamingMode.Properties.Resources.LeaguesharpStreamingModelib);
        private Int32 _leaguesharpCore = 0;
        private Dictionary<Int32, Int32> _offsets;
        enum FunctionOffset : int
        {
            DrawEvent = 0,
            PrintChat = 1,
            LoadingScreenWatermark = 2
        }

        enum Asm : byte
        {
            Ret = 0xC3,
            PushEbp = 0x55,
            Nop = 0x90
        }

        public void DoWork()
        {
            var injectedDllName = GetInjectedDllName();
            if (!string.IsNullOrEmpty(injectedDllName))
            {
                while (true)
                {
                    _leaguesharpCore = GetModuleAddress(injectedDllName.Split(new[] { "." }, StringSplitOptions.None).First());
                    try
                    {
                        if (_leaguesharpCore != 0)
                        {
                            if (_offsets == null)
                            {
                                SetUpOffsets();
                                Enable();
                            }
                            else
                            {
                                if (!IsEnabled())
                                {
                                    Enable();
                                }
                            }
                        }
                    }
                    catch (Exception)
                    {
                        
                    }
                    
                    Thread.Sleep(new TimeSpan(0, 0, 0, 5));
                }
               
            }
        }


        private int GetModuleAddress(string filename)
        {
            if (Process.GetProcessesByName("League of Legends").Length == 0)
            {
                //Console.WriteLine("Cant find League of Legends process");
                return 0;
            }
            Process P = Process.GetProcessesByName("League of Legends").First();
            for (int i = 0; i < P.Modules.Count; i++)
                if (P.Modules[i].ModuleName.Contains(filename))
                    return (Int32)(P.Modules[i].BaseAddress);
            return 0;
        }

        static byte[] ReadMemory(Int32 address, Int32 length)
        {
            var readMemory = _lib.GetType("LeaguesharpStreamingModelib.MemoryModule").GetMethods()[2];
            return (byte[])readMemory.Invoke(null, new object[] { address, length });
        }

        static void WriteMemory(Int32 address, byte value)
        {
            var writeMemory = _lib.GetType("LeaguesharpStreamingModelib.MemoryModule").GetMethods()[4];
            writeMemory.Invoke(null, new object[] { address, value });
        }

        private void WriteMemory(Int32 address, byte[] array)
        {
            var writeMemory = _lib.GetType("LeaguesharpStreamingModelib.MemoryModule").GetMethods()[4];
            writeMemory.Invoke(null, new object[] { address, array });
        }

        private int SignatureScan(int start, int length, int[] pattern)
        {
            var buffer = ReadMemory(start, length);
            for (int i = 0; i < buffer.Length - pattern.Length; i++)
            {
                if ((int)buffer[i] == pattern[0])
                {
                    for (int i2 = 1; i2 < pattern.Length; i2++)
                    {
                        if (pattern[i2] >= 0 && (int)buffer[i + i2] != pattern[i2])
                            break;
                        if (i2 == pattern.Length - 1)
                            return i;
                    }
                }
            }
            return -1;
        }



        private void SetUpOffsets()
        {
            Console.WriteLine("Set up Offsets...");
            _offsets = new Dictionary<Int32, Int32>();
            int[] pDrawEvent = { 0x55, 0x8B, 0xEC, 0x6A, 0xFF, 0x68, -1, -1, -1, -1, 0x64, 0xA1, 0, 0, 0, 0, 0x50, 0x83, 0xEC, 0x0C, 0x56, 0xA1, -1, -1, -1, -1, 0x33, 0xC5 };
            int[] pPrintChat = { 0x55, 0x8B, 0xEC, 0x8D, 0x45, 0x14, 0x50 };

            const int maxOffset = 0x50000;
            int offsetDrawEvent = SignatureScan(_leaguesharpCore, maxOffset, pDrawEvent);
            int offsetPrintChat = SignatureScan(_leaguesharpCore, maxOffset, pPrintChat);

            _offsets.Add((int)FunctionOffset.DrawEvent, offsetDrawEvent);
            _offsets.Add((int)FunctionOffset.PrintChat, offsetPrintChat);
            _offsets.Add((int)FunctionOffset.LoadingScreenWatermark, offsetPrintChat - 0x7B);
        }

        private void Enable()
        {
            WriteMemory(_leaguesharpCore + _offsets[(int)FunctionOffset.DrawEvent], (byte)Asm.Ret);
            WriteMemory(_leaguesharpCore + _offsets[(int)FunctionOffset.PrintChat], (byte)Asm.Ret);
            WriteMemory(_leaguesharpCore + _offsets[(int)FunctionOffset.LoadingScreenWatermark], new byte[] { (byte)Asm.Nop, (byte)Asm.Nop, (byte)Asm.Nop, 
                                                                                                         (byte)Asm.Nop, (byte)Asm.Nop, (byte)Asm.Nop });
            Console.WriteLine("Streaming mode activated");
        }

        private void Disable() //not used
        {
            WriteMemory(_leaguesharpCore + _offsets[(int)FunctionOffset.DrawEvent], (byte)Asm.PushEbp);
            WriteMemory(_leaguesharpCore + _offsets[(int)FunctionOffset.PrintChat], (byte)Asm.PushEbp);
        }

        private string GetInjectedDllName()
        {
            var lCorePath =
                Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "System\\Leaguesharp.Core.dll");
            if (File.Exists(lCorePath))
            {
                Console.WriteLine("Found Leaguesharp.Core.dll file. Looking for injected file in appdata...");
                var coreSize = new FileInfo(lCorePath).Length;
                var lsRoamingDir = GetLsRoamingDir();
                if (lsRoamingDir == string.Empty)
                {
                    Console.WriteLine("Cant find L# folder in Appdata");
                    return string.Empty;
                }
                var injectedPath = Path.Combine(lsRoamingDir, "1");
                var injectedFile = GetLsInjectedDll(injectedPath, coreSize);
                if (injectedFile == string.Empty)
                {
                    Console.WriteLine("Cant find L# injected dll file");
                    return string.Empty;
                }
                Console.WriteLine("Found L# injected dll file, file name: " + injectedFile);
                return injectedFile;

            }
            Console.WriteLine("Cant find Leaguesharp.Core.dll");
            return string.Empty;
        }

        public static string GetLsInjectedDll(string path, long fileLength)
        {
            foreach (var f in Directory.GetFiles(path))
            {
                var file = new FileInfo(f);
                if (file.Length == fileLength)
                    return file.Name;
            }
            return string.Empty;
        }

        /// <summary>
        /// Gets the ls roaming dir.
        /// </summary>
        /// <returns>System.String.</returns>
        /// <author>Vu Dinh</author>
        /// <modified>03/29/2015 22:46:58</modified>
        public static string GetLsRoamingDir()
        {
            var appDataDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            foreach (var dir in Directory.GetDirectories(appDataDir, "*", SearchOption.TopDirectoryOnly))
            {
                var temp = dir.Split(new[] { "\\" }, StringSplitOptions.None).Last();
                if (temp.StartsWith("LS") && temp.Length == 10)
                    return dir;
            }
            return string.Empty;
        }


        private bool IsEnabled() { return ReadMemory(_leaguesharpCore + _offsets[(int)FunctionOffset.PrintChat], 1)[0] == (byte)Asm.Ret; }

        private uint[] _hotkeys = { 0x24, 0x2D };  //home key, insert key - not used
        private uint _hotkeyOverrideNames = 35; //end key - not used

    }
}