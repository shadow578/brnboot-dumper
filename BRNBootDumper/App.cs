﻿using System;
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
            //UploadUI();
            WriteUI();
        }


        static void UploadUI()
        {
            #region User interface
            //show user interface
            SerialUserIF(out string port);
            UploadUserIF(out long targetAddress, out string filePath, out bool shouldVerify, out bool shouldVerboseLog);

            //print infos and make user confirm
            long fileSize = new FileInfo(filePath).Length;
            Console.Clear();
            Console.WriteLine($@"
Target Device: {port}

File: {filePath}
File Size: {fileSize}
Upload Target Address: 0x{ToHex(targetAddress)}
Upload End Address:    0x{ToHex(targetAddress + fileSize)}

Verify enabled: {(shouldVerify ? "YES" : "NO")} (not in XModem upload mode lol)

Press <ENTER> to start upload");
            Console.ReadLine();
            #endregion

            //initailize xmodem uploader and connect
            Console.WriteLine("initialize uploader");
            XModemUpload up = new XModemUpload();

            Console.WriteLine($"connecting to target device on {port}...");
            up.Open(port);

            // open file
            using (FileStream file = File.OpenRead(filePath))
            {

                // start upload timer
                Console.WriteLine("Start memory upload");
                Stopwatch sw = new Stopwatch();
                sw.Restart();

                // upload to device
                up.UploadToMemory(targetAddress, file);

                //stop timer and print time taken
                sw.Stop();
                Console.WriteLine($"Finished uploading to device after {Math.Floor(sw.Elapsed.TotalSeconds)} seconds");
            }
        }

        static void WriteUI()
        {
            #region User interface
            //show user interface
            SerialUserIF(out string port);
            UploadUserIF(out long targetAddress, out string filePath, out bool shouldVerify, out bool shouldVerboseLog);

            //print infos and make user confirm
            long fileSize = new FileInfo(filePath).Length;
            Console.Clear();
            Console.WriteLine($@"
Target Device: {port}

File: {filePath}
File Size: {fileSize}
Upload Target Address: 0x{ToHex(targetAddress)}
Upload End Address:    0x{ToHex(targetAddress + fileSize)}

Verify Data: {(shouldVerify ? "YES" : "NO")}
Verbose Log: {(shouldVerboseLog ? "YES" : "NO")}

Press <ENTER> to start write");
            Console.ReadLine();

            // double- confirm if any writes are outside of RAM
            if (targetAddress < BRNBootConstants.START_OF_RAM
                || targetAddress > BRNBootConstants.END_OF_RAM
                || fileSize > BRNBootConstants.RAM_SIZE)
            {
                Console.WriteLine($@"
!! WARNING !!
The Address you entered results in writes OUTSIDE of RAM
RAM Start: {ToHex(BRNBootConstants.START_OF_RAM)}
RAM End: {ToHex(BRNBootConstants.END_OF_RAM)}
RAM Size: {BRNBootConstants.RAM_SIZE}

Press <ENTER> 3x to write anyways");
                Console.ReadLine();
                Console.ReadLine();
                Console.ReadLine();
            }
            #endregion

            //initailize memory writer and connect
            Console.WriteLine("initialize writer");
            CrappyUpload up = new CrappyUpload
            {
                EnableDebugPrints = shouldVerboseLog
            };

            Console.WriteLine($"connecting to target device on {port}...");
            up.Open(port);

            // open file
            using (FileStream file = File.OpenRead(filePath))
            {
                // start upload timer
                Console.WriteLine("Start memory write...");
                Stopwatch sw = new Stopwatch();
                sw.Restart();

                // write to device
                up.Write(file, targetAddress);

                //stop timer and print time taken
                sw.Stop();
                up.Close();
                Console.WriteLine($"Finished writing to device after {Math.Floor(sw.Elapsed.TotalSeconds)} seconds");

                // verify what was actually written if enabled
                if (shouldVerify)
                {
                    // initialize dumper and connect
                    Console.WriteLine("Verifying written data...");
                    Dumper d = new Dumper
                    {
                        EnableDebugPrints = shouldVerboseLog
                    };

                    Console.WriteLine($"connecting to target device on {port}...");
                    d.Open(port);

                    // dump the memory
                    Console.WriteLine("Dumping memory...");
                    DumpTimed(d, targetAddress, targetAddress + fileSize, 10000, out MemoryStream verify);
                    d.Close();

                    // compare streams
                    if (CheckStreamsEqual(file, verify))
                        Console.WriteLine("Verification Success");
                    else
                        Console.WriteLine("!! Verification failed!");
                }
            }
        }

        static void DumperUI()
        {
            #region user interface
            //show user interface
            SerialUserIF(out string port);
            DumperUserIF(out long startAddress, out long endAddress, out int blockSize, out bool shouldVerifyDump, out bool shouldVerboseLog);

            //print infos and make user confirm
            long dumpLenghtBytes = endAddress - startAddress;
            Console.Clear();
            Console.WriteLine($@"
Target Device: {port}

Will dump {dumpLenghtBytes} bytes
Start Address: 0x{ToHex(startAddress)}
End Address  : 0x{ToHex(endAddress)}
Block size   : {blockSize}

Dump verification is {(shouldVerifyDump ? "ON" : "OFF")}
Verbose logging is {(shouldVerboseLog ? "ON" : "OFF")}
Dumping will take {(dumpLenghtBytes / blockSize) * (shouldVerifyDump ? 2 : 1)} calls

Press <ENTER> to start dumping");
            Console.ReadLine();
            #endregion

            // initialize dumper and connect
            Console.WriteLine("initialize dumper...");
            Dumper d = new Dumper
            {
                EnableDebugPrints = shouldVerboseLog
            };

            Console.WriteLine($"connecting to target device on {port}...");
            d.Open(port);

            // dump the memory
            Console.WriteLine("Dumping memory...");
            DumpTimed(d, startAddress, endAddress, blockSize, out MemoryStream dump);

            // verification, if enabled
            if (shouldVerifyDump)
            {
                // dump memory a second time
                Console.WriteLine("Dumping memory, verification pass...");
                DumpTimed(d, startAddress, endAddress, blockSize, out MemoryStream verify);

                // check both dumps are equal, byte- by- byte
                if (!CheckStreamsEqual(dump, verify))
                {
                    Console.WriteLine("!! Dump verification failed!");
                }
            }

            // write dump to file
            Console.WriteLine("Writing dump to file...");
            Stopwatch sw = new Stopwatch();
            sw.Restart();

            // write to file
            using (MemoryStream source = dump)
            using (FileStream target = File.Create("dump.bin"))
            {
                source.Seek(0, SeekOrigin.Begin);
                source.CopyTo(target);
            }

            //stop timer and print time taken
            sw.Stop();
            Console.WriteLine($"Finished writing to file after {Math.Floor(sw.Elapsed.TotalSeconds)} seconds");
        }

        /// <summary>
        /// dump flash using the given dumper, print time taken after dump finished
        /// </summary>
        /// <param name="d">the dumper to dump with</param>
        /// <param name="startAddress">the start address</param>
        /// <param name="endAddress">the end address</param>
        /// <param name="blockSize">the block size for dumping</param>
        /// <param name="output">the output memory stream</param>
        static void DumpTimed(Dumper d, long startAddress, long endAddress, int blockSize, out MemoryStream output)
        {
            //prepare output stream first
            output = new MemoryStream();

            //start timer
            Stopwatch sw = new Stopwatch();
            sw.Restart();

            // dump
            int errorCount = d.DumpAdr(output, startAddress, endAddress, blockSize);

            //stop timer and print time taken
            sw.Stop();
            Console.WriteLine($"finished dumping {output.Length} bytes after {Math.Floor(sw.Elapsed.TotalSeconds)} seconds with {errorCount} errors");

            //warn if there were errors while dumping
            if (errorCount != 0)
                Console.WriteLine("!! Warning: there were errors while dumping");
        }

        /// <summary>
        /// show a interactive user interface to get dump parameters
        /// </summary>
        /// <param name="startAddress">the start address of the dump</param>
        /// <param name="endAddress">the end address of the dump</param>
        /// <param name="blockSize">the block size for dumping</param>
        /// <param name="doVerifyDump">should we verify the dumped contents?</param>
        /// <param name="doVerboseLogging">should we write verbose log to console?</param>
        static void DumperUserIF(out long startAddress, out long endAddress,
            out int blockSize,
            out bool doVerifyDump, out bool doVerboseLogging)
        {
            #region start / end address
            // ask for start and end address
            string tmp;
            do
            {
                tmp = UserInput("Dump start Address> 0x");
            } while (!long.TryParse(tmp, NumberStyles.HexNumber, null, out startAddress));

            do
            {
                tmp = UserInput("Dump end Address> 0x");
            } while (!long.TryParse(tmp, NumberStyles.HexNumber, null, out endAddress));

            //ask for dump block size, default to 1024
            blockSize = 1024;
            tmp = UserInput("Dump block size (16 - 10000)[1024]>");
            if (int.TryParse(tmp, out int bs)
                && bs >= 16
                && bs <= 10000)
            {
                blockSize = bs;
            }
            #endregion

            #region verify dump
            Console.WriteLine("Do you want to verify the dumped flash contents? (Will take twice as long)");
            Console.Write("[Y/n]: ");
            tmp = Console.ReadLine();
            doVerifyDump = !tmp.Equals("n", StringComparison.OrdinalIgnoreCase);
            #endregion

            #region verbose log
            Console.WriteLine("Do you want to enable verbose output?");
            Console.Write("[y/N]: ");
            tmp = Console.ReadLine();
            doVerboseLogging = tmp.Equals("y", StringComparison.OrdinalIgnoreCase);
            #endregion
        }

        /// <summary>
        /// show a interactive user interface to get upload parameters
        /// </summary>
        /// <param name="targetAddress">the address to upload to</param>
        /// <param name="uploadPath">the file to upload</param>
        /// <param name="verifyWrite">should the written data be verified?</param>
        /// <param name="verboseLogging">should we write verbose log to console?</param>
        static void UploadUserIF(out long targetAddress, out string uploadPath, out bool verifyWrite, out bool verboseLogging)
        {
            #region target address
            // ask for target  address
            string tmp;
            do
            {
                tmp = UserInput($"Upload Target Address (default 0x{ToHex(BRNBootConstants.START_OF_RAM)})> 0x");
            } while (!long.TryParse(tmp, NumberStyles.HexNumber, null, out targetAddress));
            #endregion

            #region file select
            //ask for target file path
            do
            {
                uploadPath = UserInput("Upload file path > ");
            } while (!File.Exists(uploadPath));
            #endregion

            #region verify write
            Console.WriteLine("Do you want to verify the written data? (Will take twice as long)");
            Console.Write("[Y/n]: ");
            tmp = Console.ReadLine();
            verifyWrite = !tmp.Equals("n", StringComparison.OrdinalIgnoreCase);
            #endregion

            #region verbose log
            Console.WriteLine("Do you want to enable verbose output?");
            Console.Write("[y/N]: ");
            tmp = Console.ReadLine();
            verboseLogging = tmp.Equals("y", StringComparison.OrdinalIgnoreCase);
            #endregion
        }

        /// <summary>
        /// show a interactive user interface to get target device port
        /// </summary>
        /// <param name="port">the serial port, is contained in SerialPort.GetPortNames()</param>
        static void SerialUserIF(out string port)
        {
            #region Serial port
            // list ports to user
            Console.WriteLine("Choose the Serial port the device is connected to. \nAvailable Ports are:");
            foreach (string p in SerialPort.GetPortNames())
                Console.WriteLine($" {p}");

            // show warning if there are no ports
            if (SerialPort.GetPortNames().Length <= 0)
                Console.WriteLine(" No ports available!");

            // read a VALID port name
            do
            {
                port = UserInput("Target Port> ");
            } while (!SerialPort.GetPortNames().Contains(port));
            #endregion
        }

        /// <summary>
        /// check if two streams are equal (byte- by- byte)
        /// </summary>
        /// <param name="a">the first stream</param>
        /// <param name="b">the second stream</param>
        /// <returns>are both streams equal?</returns>
        static bool CheckStreamsEqual(Stream a, Stream b)
        {
            // disallow null values
            if (a == null || b == null)
                throw new ArgumentNullException(a == null ? "a" : "b");

            // same reference? they are equal
            if (a == b)
                return true;

            // different lenght? cannot be equal
            if (a.Length != b.Length)
                return false;

            // seek both streams to the beginning
            a.Seek(0, SeekOrigin.Begin);
            b.Seek(0, SeekOrigin.Begin);

            // test byte- by- byte
            for (int i = 0; i < a.Length; i++)
            {
                int aByte = a.ReadByte();
                int bByte = b.ReadByte();
                if (aByte.CompareTo(bByte) != 0)
                {
                    return false;
                }
            }

            return true;
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
