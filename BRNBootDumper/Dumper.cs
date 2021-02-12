using System;
using static BRNBootDumper.DumperState;
using static BRNBootDumper.BRNBootConstants;
using System.Diagnostics;
using System.Threading;
using System.IO;
using System.Globalization;

namespace BRNBootDumper
{
    class Dumper
    {
        /// <summary>
        /// should debug prints be enabled (to console)
        /// </summary>
        public bool EnableDebugPrints { get; set; } = true;

        /// <summary>
        /// serial connection to brnboot device
        /// </summary>
        private SerialPortWrapper serial;

        /// <summary>
        /// open the serial connection to the target device
        /// </summary>
        /// <param name="serialPortName">the serial port to connect with</param>
        public void Open(string serialPortName)
        {
            // init and open port
            DB($"opening serial port at {serialPortName} with {BRN_BAUD_RATE} baud...");
            serial = new SerialPortWrapper(serialPortName, BRN_BAUD_RATE);
            serial.RegisterHandlers();
            serial.Open();

            // check port is now open
            if (!serial.IsOpen)
                throw new InvalidOperationException("Serial port could not be opened!");
        }

        /// <summary>
        /// dump memory of the device, starting at the startAddr until the endAddr.
        /// </summary>
        /// <param name="output">where to write the dupted memory to</param>
        /// <param name="startAddr">address to start dumping at</param>
        /// <param name="endAddr">address to end dumping</param>
        /// <param name="blockSize">how many bytes to dump in one go. has to be between 1 and 10000</param>
        /// <returns>dump error count</returns>
        public int DumpAdr(Stream output, long startAddr, long endAddr, int blockSize = 1024)
        {
            return DumpCnt(output, startAddr, endAddr - startAddr, blockSize);
        }

        /// <summary>
        /// dump count bytes from the memory of the device, starting at the startAddr
        /// </summary>
        /// <param name="output">where to write the dupted memory to</param>
        /// <param name="startAddr">address to start dumping at</param>
        /// <param name="count">how many bytes to dump</param>
        /// <param name="blockSize">how many bytes to dump in one go. has to be between 1 and 10000</param>
        /// <returns>dump error count</returns>
        public int DumpCnt(Stream output, long startAddr, long count, int blockSize = 1024)
        {
            //check count is valid
            if (count < 0)
                throw new ArgumentException("count cannot be less than 0!");

            // check blocksize is valid
            if (blockSize <= 0 || blockSize > 10000)
                throw new ArgumentException("blocksize must be 0 < blockSize < 10000");

            // check port is open
            if (!serial.IsOpen)
                throw new InvalidOperationException("Serial port is not open! call Open() first!");

            // start by sending two empty commands to get back to prompt
            DB("returning to prompt...");
            SendWithEnter("");
            SendWithEnter("");
            ClearAll();

            // wait for prompt to appear, then enter administrator mode
            DB("enter admin mode...");
            WaitForState(IDLE);
            ClearAll();
            Send(CMD_ADMIN_MODE);

            // start dumping
            long currentAddress = startAddr;
            long bytesLeftToRead = count;
            long block;
            int dumpErrorCount = 0;
            while (bytesLeftToRead > 0)
            {
                // enter read mode
                DB("enter read mode...");
                WaitForState(IDLE);
                ClearAll();
                Send(CMD_READ_MODE);

                // enter start address
                DB("set start address");
                WaitForState(START_ADDR);
                ClearAll();
                SendWithEnter(ToHex(currentAddress));

                // choose data size 1 bytes
                DB("set data size...");
                WaitForState(DATA_LEN);
                ClearAll();
                Send("3");

                // calculate block size
                block = Math.Min(blockSize, bytesLeftToRead);

                // enter bytes to read
                DB($"set dump size (to {block})...");
                WaitForState(DUMP_COUNT);
                SendWithEnter(block.ToString());

                // all following lines till the brnboot prompt are the data dumped
                DB("start dumping...");
                int totalRead = 0;
                do
                {
                    //dump all lines
                    while (serial.HasNextLine())
                    {
                        string ln = serial.NextLine();
                        totalRead += ParseLine(output, ln);

                        //show progress
                        PrintProgress(currentAddress, count, count - (bytesLeftToRead - totalRead));
                    }
                }
                while (ParseState(serial.PeekCurrentLine()) != IDLE);

                // check we actually read the full block
                if (totalRead != block)
                {
                    DB($"!! read different number than wanted (WANTED= {block} GOT= {totalRead})");
                    dumpErrorCount++;
                }

                // update current address and bytes to read
                DB("dumping done, update state...");
                currentAddress += totalRead;
                bytesLeftToRead -= totalRead;
            }

            // finish with two empty commands to get back to prompt
            DB("dump finished, return to prompt");
            SendWithEnter("");
            SendWithEnter("");
            ClearAll();

            DB($"dumper finished, with {dumpErrorCount} errors");
            return dumpErrorCount;
        }

        /// <summary>
        /// parse a line from the memory dumping page. checks if the line matches the expected format before processing
        /// </summary>
        /// <param name="output">the output stream to write parsed bytes to</param>
        /// <param name="ln">the line to parse</param>
        /// <returns>the number of bytes that were parsed from the line</returns>
        int ParseLine(Stream output, string ln)
        {
            // make sure the line is not empty
            if (string.IsNullOrWhiteSpace(ln))
                return 0;

            // split line on spaces
            string[] segments = ln.Split(' ');

            // we should have AT LEAST two segements now
            if (segments.Length < 2)
                return 0;

            // first segment should be memory offset, test this is true by checking for 0x prefix
            if (!segments[0].Trim().StartsWith("0x"))
                return 0;

            // all but the first segment are data, one byte each
            // dump them one- by- one
            int byteCount = 0;
            for (int i = 1; i < segments.Length - 1; i++)
            {
                // parse segemnt into a byte
                string segment = segments[i];
                if (!byte.TryParse(segment, NumberStyles.HexNumber, null, out byte value))
                    throw new InvalidDataException($"Could not parse segment {i}: {segment} of input {ln}");

                // write the byte to the output
                output.WriteByte(value);
                byteCount++;
            }

            // print memory offset and parsing information
            DB($"Parsed {byteCount} bytes at Memory Offset {segments[0]}; {segments.Length} segments ({segments.Length - 2} data)");
            return byteCount;
        }

        /// <summary>
        /// clear all buffers in serial
        /// </summary>
        void ClearAll()
        {
            while (serial.HasNextLine())
                serial.NextLine();

            serial.ClearCurrentLine();
        }

        /// <summary>
        /// wait until the device sends a response that is parsed by ParseState to the given target state
        /// </summary>
        /// <param name="target">the target state</param>
        /// <param name="timeout">timeout, in seconds</param>
        void WaitForState(DumperState target, int timeout = 10)
        {
            Stopwatch w = new Stopwatch();
            w.Start();
            void CheckTimeout()
            {
                if (w.Elapsed.TotalSeconds > timeout)
                    throw new TimeoutException("timeout while waiting for state!");
            }

            //continuously parse state using the current line
            while (ParseState(serial.PeekCurrentLine()) != target)
            {
                Thread.Sleep(1);
                CheckTimeout();
            }
        }

        /// <summary>
        /// send a string to the device using the serial connection, and append a ENTER character
        /// </summary>
        /// <param name="s">the string to send</param>
        void SendWithEnter(string s)
        {
            Send(s + BRN_ENTER);
        }

        /// <summary>
        /// send a string to the device using the serial connection
        /// </summary>
        /// <param name="s">the string to send</param>
        void Send(string s)
        {
            serial.Write(s);
        }

        /// <summary>
        /// parse the current state of the device using a line received from the device
        /// </summary>
        /// <param name="ln">the line received from the device</param>
        /// <returns>the parsed state. if unknown, UNKNOWN state is returned</returns>
        DumperState ParseState(string ln)
        {
            if (ln.EndsWith(PATTERN_PROMPT))
                return IDLE;
            else if (ln.Contains(PATTERN_START_ADDR, StringComparison.OrdinalIgnoreCase))
                return START_ADDR;
            else if (ln.Contains(PATTERN_DATA_LENGHT, StringComparison.OrdinalIgnoreCase))
                return DATA_LEN;
            else if (ln.Contains(PATTERN_READ_COUNT, StringComparison.OrdinalIgnoreCase))
                return DUMP_COUNT;

            return UNKNOWN;
        }

        /// <summary>
        /// utility to convert a number into a hex string, without 0x prefix
        /// </summary>
        /// <param name="n">the number to convert</param>
        /// <returns>the hex string, without 0x prefix</returns>
        string ToHex(long n)
        {
            return n.ToString("X");
        }

        /// <summary>
        /// print progress info to console
        /// </summary>
        /// <param name="currentAddress">the address we're currently dumping at</param>
        /// <param name="totalBytesToRead">the total number of bytes we have to read</param>
        /// <param name="totalBytesRead">the total number of bytes we have already read</param>
        void PrintProgress(long currentAddress, long totalBytesToRead, long totalBytesRead)
        {
            //print progress to console title when debug prints are enabled (could not read anything anyways)
            if (EnableDebugPrints)
                Console.Title = $"ADR 0x{ToHex(currentAddress),-10} | READ {totalBytesRead,-10} bytes | PROGRESS: {(totalBytesRead * 100) / totalBytesToRead}%";
            else
                Console.Write($"ADR 0x{ToHex(currentAddress),-10} | READ {totalBytesRead,-10} bytes | PROGRESS: {(totalBytesRead * 100) / totalBytesToRead}% \r");
        }

        /// <summary>
        /// Debug print to console
        /// </summary>
        /// <param name="ln">line to print</param>
        void DB(string ln)
        {
            if (EnableDebugPrints)
                Console.WriteLine(ln);
        }
    }
}
