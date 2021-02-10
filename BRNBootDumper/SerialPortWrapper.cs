using System.Collections.Generic;
using System.IO.Ports;
using System.Text;

namespace BRNBootDumper
{
    class SerialPortWrapper : SerialPort
    {
        /// <summary>
        /// contains all received lines, excluding the current Line
        /// </summary>
        readonly Queue<string> linesBuffer = new Queue<string>();

        /// <summary>
        /// the line that is currently beign written. as soon as we get a NewLine, this is written into linesBuffer and cleared
        /// </summary>
        readonly StringBuilder currentLine = new StringBuilder();

        public SerialPortWrapper(string p, int baud) : base(p, baud)
        {
        }

        /// <summary>
        /// register data receive handlers
        /// </summary>
        public void RegisterHandlers()
        {
            DataReceived += OnDataReceived;
        }

        /// <summary>
        /// this.DataReceived callback
        /// </summary>
        void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            //read all characters into currentLine, push lines into linesBuffer as needed
            while (BytesToRead > 0)
                lock (currentLine)
                    lock (linesBuffer)
                    {
                        // read char, write to current line
                        char c = (char)ReadChar();
                        currentLine.Append(c);

                        // if char is \n, push current line into line buffer
                        if (c == '\n')
                        {
                            linesBuffer.Enqueue(currentLine.ToString());
                            currentLine.Clear();
                        }

                        // write to debug output
                        //Debug.Write(c);
                    }
        }

        /// <summary>
        /// do we have a line to read, using NextLine()
        /// </summary>
        /// <returns>do we have a line to read?</returns>
        public bool HasNextLine()
        {
            lock (linesBuffer)
                return linesBuffer.Count > 0;
        }

        /// <summary>
        /// read the oldest line from the line buffer.
        /// </summary>
        /// <returns>the line read</returns>
        public string NextLine()
        {
            lock (linesBuffer)
                return linesBuffer.Dequeue();
        }

        /// <summary>
        /// peek the line that is currently beign received.
        /// Useful if a message is written without a NEWLINE at the end.
        /// </summary>
        /// <returns>the current line</returns>
        public string PeekCurrentLine()
        {
            lock (currentLine)
                return currentLine.ToString();
        }

        /// <summary>
        /// clear the current line
        /// </summary>
        public void ClearCurrentLine()
        {
            lock (currentLine)
                currentLine.Clear();
        }
    }
}
