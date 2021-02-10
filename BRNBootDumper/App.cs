using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Linq;

namespace BRNBootDumper
{
    class App
    {
        public static void Main()
        {
            // list ports to user
            Console.WriteLine("Choose the Serial port the device is connected to. \nAvailable Ports are:");
            foreach (string p in SerialPort.GetPortNames())
                Console.WriteLine($" {p}");

            // show warning if there are no ports
            if (SerialPort.GetPortNames().Length <= 0)
                Console.WriteLine(" No ports available!");

            // read a VALID port name
            string port;
            do
            {
                port = UserInput("Target Port> ");
            } while (!SerialPort.GetPortNames().Contains(port));

            // ask for start and end address
            long startAddr, endAddr;
            string tmp;
            do
            {
                tmp = UserInput("Dump start Address> 0x");
            } while (!long.TryParse(tmp, NumberStyles.HexNumber, null, out startAddr));

            do
            {
                tmp = UserInput("Dump end Address> 0x");
            } while (!long.TryParse(tmp, NumberStyles.HexNumber, null, out endAddr));

            //ask for dump block size, default to 1024
            int blockSize = 1024;
            tmp = UserInput("Dump block size (16 - 10000)[1024]>");
            if(int.TryParse(tmp, out int bs)
                && bs >= 16
                && bs <= 10000)
            {
                blockSize = bs;
            }

            // print info
            Console.WriteLine($@"
Dumping {endAddr - startAddr} bytes,
starting at 0x{ToHex(startAddr)}
ending at   0x{ToHex(endAddr)}
block size: {blockSize} (will require {(endAddr - startAddr) / blockSize} calls)

Press <ENTER> to start");
            Console.ReadLine();


            // init dumper and start dumping
            Console.WriteLine("initialize dumper...");
            Dumper d = new Dumper();
            d.Open(port);

            Console.WriteLine("Start dumping...");
            Stopwatch sw = new Stopwatch();
            sw.Start();

            MemoryStream ms = new MemoryStream();
            d.DumpAdr(ms, startAddr, endAddr, blockSize);

            sw.Stop();
            Console.WriteLine($"Dumping done after {sw.Elapsed.TotalSeconds} s");

            // write dump to file
            sw.Restart();
            Console.WriteLine($"Dumped total of {ms.Length} bytes");
            Console.WriteLine("Writing dump to file ./dump.bin");
            using (MemoryStream source = ms)
            using (FileStream target = File.Create("dump.bin"))
            {
                source.Seek(0, SeekOrigin.Begin);
                source.CopyTo(target);
            }

            Console.WriteLine("Done Writing");
        }

        /// <summary>
        /// utility to convert a number into a hex string, without 0x prefix
        /// </summary>
        /// <param name="n">the number to convert</param>
        /// <returns>the hex string, without 0x prefix</returns>
        static string ToHex(long n)
        {
            return n.ToString("X");
        }

        /// <summary>
        /// read a non- empty line from the user
        /// </summary>
        /// <param name="msg">the message to show to the user</param>
        /// <returns>the line the user entered</returns>
        static string UserInput(string msg)
        {
            string o;
            do
            {
                Console.Write(msg);
                o = Console.ReadLine();
            } while (string.IsNullOrWhiteSpace(o));

            return o;
        }
    }
}
