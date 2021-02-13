using System;
using static BRNBootDumper.DumperState;
using static BRNBootDumper.BRNBootConstants;
using System.Diagnostics;
using System.Threading;
using System.IO;
using System.Text;

namespace BRNBootDumper
{
    class CrappyUpload
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
        /// close the serial connection to the target device
        /// </summary>
        public void Close()
        {
            if (serial.IsOpen)
                serial.Close();
        }

        /// <summary>
        /// write the given data stream to the device's memory using brnboot write command
        /// </summary>
        /// <param name="toWrite">the data stream to write</param>
        /// <param name="startAddr">the address the first byte is written to</param>
        public void Write(Stream toWrite, long startAddr)
        {
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

            // wait for IDLE state before we go into the main loop
            DB("wait for idle state...");
            WaitForState(IDLE);

            // read data, 4 bytes at a time
            long currentAddress = startAddr;
            long totalWritten = 0;
            byte[] dataBuffer = new byte[4];
            int read;
            while ((read = toWrite.Read(dataBuffer, 0, dataBuffer.Length)) > 0)
            {
                // print progress info
                PrintProgress(currentAddress, totalWritten, toWrite.Length);

                // pad data buffer if we read less than 4 bytes (near end of file)
                if (read < dataBuffer.Length)
                    for (int i = read; i < dataBuffer.Length; i++)
                        dataBuffer[i] = 0xFF;

                // allow up to 5 retries for write
                for (int retry = 0; retry < 5; retry++)
                {
                    // enter memory write mode
                    DB("enter write mode");
                    ClearAll();
                    Send(CMD_MEMORY_WRITE);

                    // enter start address
                    DB("set start address");
                    WaitForState(WRITE_START_ADDR);
                    ClearAll();
                    SendWithEnter(ToHex(currentAddress));

                    // choose data size 4 bytes (to write 4 bytes at once, instead of one)
                    DB("set write data size");
                    WaitForState(DATA_LEN);
                    ClearAll();
                    Send("1");

                    // set write repeats (called "count to write", which is kinda right, 
                    // but if set to 2 the data entered is just repeated... which is not usefull for us)
                    DB("set data count");
                    WaitForState(WRITE_COUNT);
                    ClearAll();
                    SendWithEnter("1");

                    // send data to write, 4 bytes at once
                    DB("send data");
                    WaitForState(WRITE_DATA_PROMPT);
                    ClearAll();
                    SendWithEnter(ToHex(dataBuffer));

                    // wait until we get a "Writing Process Completed" message
                    DB("wait for write to complete...");
                    try
                    {
                        WaitForState(WRITE_COMPLETED);
                    }
                    catch (TimeoutException)
                    { 
                        DB("Timeout while waiting for WRITE_COMPLETED. write possibly failed, retry");
                        ClearAll();
                        SendWithEnter("");
                        continue;
                    }
                }

                // wait 1ms before next write, to make sure we're back at the prompt
                Thread.Sleep(1);
                break;

                // update address and bytes written
                currentAddress += dataBuffer.Length;
                totalWritten += dataBuffer.Length;
            }

            // finish with two empty commands to get back to prompt
            DB("dump finished, return to prompt");
            SendWithEnter("");
            SendWithEnter("");
            ClearAll();

            DB($"write finished");
        }

        /// <summary>
        /// converts the byte array to a hex string
        /// </summary>
        /// <param name="data">the data to convert to hex</param>
        /// <returns>the hex string</returns>
        string ToHex(byte[] data)
        {
            StringBuilder sb = new StringBuilder();
            foreach (byte b in data)
                sb.Append(ToHex(b));

            return sb.ToString();
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
            else if (ln.Contains(PATTERN_WRITE_START_ADDR, StringComparison.OrdinalIgnoreCase))
                return WRITE_START_ADDR;
            else if (ln.Contains(PATTERN_DATA_LENGHT, StringComparison.OrdinalIgnoreCase))
                return DATA_LEN;//shared between read and write
            else if (ln.Contains(PATTERN_WRITE_COUNT, StringComparison.OrdinalIgnoreCase))
                return WRITE_COUNT;
            else if (ln.Contains(PATTERN_DATA_TO_WRITE, StringComparison.OrdinalIgnoreCase))
                return WRITE_DATA_PROMPT;
            else if (ln.Contains(PATTERN_WRITE_COMPLETED, StringComparison.OrdinalIgnoreCase))
                return WRITE_COMPLETED;

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
        /// <param name="currentAddress">the address we're currently writing to</param>
        /// <param name="totalWritten">the total number of bytes we have written already</param>
        /// <param name="totalToWrite">the total number of bytes we have to write</param>
        void PrintProgress(long currentAddress, long totalWritten, long totalToWrite)
        {
            //print progress to console title when debug prints are enabled (could not read anything anyways)
            if (EnableDebugPrints)
                Console.Title = $"ADR 0x{ToHex(currentAddress),-10} | WROTE {totalWritten,-10} bytes | PROGRESS: {(totalWritten * 100) / totalToWrite}%";
            else
                Console.Write($"ADR 0x{ToHex(currentAddress),-10} | WROTE {totalWritten,-10} bytes | PROGRESS: {(totalWritten * 100) / totalToWrite}% \r");
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
