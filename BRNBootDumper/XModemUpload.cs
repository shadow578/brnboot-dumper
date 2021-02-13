using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using static BRNBootDumper.DumperState;
using static BRNBootDumper.BRNBootConstants;
using static BRNBootDumper.XModemConstants;
using System.Threading;
using System.Diagnostics;
using System.Linq;

namespace BRNBootDumper
{
    class XModemUpload
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
        /// use brnboot Upload to memory (M) command to upload data to the device
        /// </summary>
        /// <param name="targetAddress">the target address to upload to</param>
        /// <param name="data">the data to upload</param>
        /// <returns>was the upload successfull?</returns>
        public bool UploadToMemory(long targetAddress, Stream data)
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

            // enter XModem upload mode
            DB("enter memory upload mode...");
            WaitForState(IDLE);
            ClearAll();
            Send(CMD_MEMORY_UPLOAD);

            // enter target address for upload
            DB("send upload target address");
            WaitForState(UPLOAD_ADDR);
            ClearAll();
            SendWithEnter(ToHex(targetAddress));

            // wait until device is ready for upload
            DB("Waiting until device is ready for upload...");
            WaitForChar(10, MODEM_WAIT_FOR_CONNECTION);

            // prepare xmodem upload
            byte[] packetPayload = new byte[MODEM_DATA_SIZE];
            byte[] packet = new byte[MODEM_PACKET_SIZE];
            int packetCount = 0;
            int read;
            bool packetFailed = false;
            while ((read = data.Read(packetPayload, 0, MODEM_DATA_SIZE)) > 0)
            {
                // pad packet payload to MODEM_DATA_SIZE bytes
                if (read < MODEM_DATA_SIZE)
                {
                    DB($"Pad data payload: {MODEM_DATA_SIZE - read} to {MODEM_DATA_SIZE}");
                    for (int i = read; i < MODEM_DATA_SIZE; i++)
                        packetPayload[i] = MODEM_PADDING;
                }

                // calculate block number for packet (0-255)
                packetCount++;
                byte blockNo = (byte)(packetCount % 256);

                // build packet
                BuildModemPacket(blockNo, packetPayload, ref packet);

                // send packet, 10 retries
                //for (int retry = 0; retry < 10; retry++)
                for (int retry = 0; ; retry++)
                {
                    // write progress
                    PrintProgress(packetCount, retry, data.Length);

                    // send to device
                    DB($"Send Packet No: {packetCount} retry: {retry} block: {blockNo}");
                    serial.Write(packet, 0, MODEM_PACKET_SIZE);

                    // wait for ACK or NACK
                    char rec = WaitForChar(10, MODEM_ACK, MODEM_NAK);

                    // retry send on NAK
                    if (rec == MODEM_ACK)
                    {
                        DB("Received MODEM_ACK, continue with next block");
                        packetFailed = false;
                        break;
                    }
                    else if (rec == MODEM_NAK)
                    {
                        DB("Received MODEM_NAK, retry block");
                        //TODO: ignore failed blocks,
                        packetFailed = false;
                        break;
                        
                        //packetFailed = true;
                        //continue;
                    }
                    else
                        DB($"received unknown char from WaitForChar: {rec} {(int)rec}");// this will never happen

                }

                // if this packet failed after 10 retries, abort sending
                if (packetFailed)
                {
                    DB($"Packet {packetCount} failed after 10 retries, aborting upload!");
                    return false;
                }
            }

            // send end of transmission
            DB("done, send EOT");
            serial.Write("" + MODEM_EOT);


            //TODO: execute code
            //Thread.Sleep(100);
            //SendWithEnter("Y");

            //TODO: wait 10s for messages from device
            for(int i = 0; i < 2000; i++)
            {
                Thread.Sleep(1);
            }

            // finish with two empty commands to get back to prompt
            //DB("dump finished, return to prompt");
            //SendWithEnter("");
            //SendWithEnter("");
            //ClearAll();

            DB($"upload finished");
            return true;
        }

        /// <summary>
        /// build a xmodem packet from the given data and block number
        /// </summary>
        /// <param name="block">the block number to set</param>
        /// <param name="data">the data payloud, lenght has to be <see cref="MODEM_DATA_SIZE"/></param>
        /// <param name="packet">the packet buffer to write to, lenght has to be <see cref="MODEM_PACKET_SIZE"/></param>
        void BuildModemPacket(byte block, byte[] data, ref byte[] packet)
        {
            // check data payload lenght is right
            if (data.Length != MODEM_DATA_SIZE)
                throw new ArgumentException($"data has to be {MODEM_DATA_SIZE} bytes in lenght!");

            // check packet target is right lenght
            if (packet.Length != MODEM_PACKET_SIZE)
                throw new ArgumentException($"packet target buffer has to be {MODEM_PACKET_SIZE} bytes in lenght!");

            // build header:
            // at 0x0: SOH
            packet[0] = (byte)MODEM_SOH;

            // at 0x1: block number
            packet[1] = block;

            // at 0x2: block number, ones compliment
            packet[2] = (byte)~block;

            // at 0x3: data
            Array.Copy(data, 0, packet, 3, MODEM_DATA_SIZE);

            // at 0x83: checksum
            packet[131] = Checksum(data);
        }

        /// <summary>
        /// calculate a xmodem checksum
        /// </summary>
        /// <param name="data">the data to get the checksum of</param>
        /// <returns>the xmodem checksum</returns>
        byte Checksum(byte[] data)
        {
            int sum = 0;
            foreach (byte b in data)
                sum += b;

            return (byte)(sum % 256);
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
        /// wait until the device sends one of the given chars
        /// </summary>
        /// <param name="timeout">timeout, in seconds</param>
        /// <param name="target">the target chars. if any of these chars is received, the wait ends</param>
        /// <returns>the char the device sends. is part of target</returns>
        char WaitForChar(int timeout = 10, params char[] target)
        {
            Stopwatch w = new Stopwatch();
            w.Start();
            void CheckTimeout()
            {
                if (w.Elapsed.TotalSeconds > timeout)
                    throw new TimeoutException("timeout while waiting for upload ready!");
            }

            //continuously check if device sends one of the target chars
            while (true)
            {
                // get current line and clear it immediately
                string ln = serial.PeekCurrentLine();
                serial.ClearCurrentLine();

                // check if ANY char in the line matches one of the targets
                foreach (char c in ln)
                    if (target.Contains(c))
                        return c;

                // wait a sec and check timeout
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
            else if (ln.Contains(PATTERN_READ_START_ADDR, StringComparison.OrdinalIgnoreCase))
                return DUMP_START_ADDR;
            else if (ln.Contains(PATTERN_DATA_LENGHT, StringComparison.OrdinalIgnoreCase))
                return DATA_LEN;
            else if (ln.Contains(PATTERN_READ_COUNT, StringComparison.OrdinalIgnoreCase))
                return DUMP_COUNT;
            else if (ln.Contains(PATTERN_MEMORY_UPLOAD_TARGET_ADDR_PROMPT, StringComparison.OrdinalIgnoreCase))
                return UPLOAD_ADDR;

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
        /// <param name="blockNo">the REAL number of blocks  (can be > 255)</param>
        /// <param name="blockRetry">on what retry is the current block?</param>
        /// <param name="totalBytesToSend">how many bytes do we send in total?</param>
        void PrintProgress(int blockNo, int blockRetry, long totalBytesToSend)
        {
            //print progress to console title when debug prints are enabled (could not read anything anyways)
            if (EnableDebugPrints)
                Console.Title = $"BLOCK: {blockNo} @ RETRY {blockRetry} | SENT: {blockNo * MODEM_DATA_SIZE} | LEFT: {totalBytesToSend} | PROGRESS {(blockNo * MODEM_DATA_SIZE * 100) / totalBytesToSend}%";
            else
                Console.Write($"BLOCK: {blockNo} @ RETRY {blockRetry} | SENT: {blockNo * MODEM_DATA_SIZE} | LEFT: {totalBytesToSend} | PROGRESS {(blockNo * MODEM_DATA_SIZE * 100) / totalBytesToSend}% \r");

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
