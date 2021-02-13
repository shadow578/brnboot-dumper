namespace BRNBootDumper
{
    enum DumperState
    {
        /// <summary>
        /// we're currently in a unknown state, send empty command to figure state out
        /// </summary>
        UNKNOWN,

        /// <summary>
        /// currently sitting at brnboot console
        /// </summary>
        IDLE,

        /// <summary>
        /// sitting at start address prompt
        /// (Enter the Start Address to Read....0x)
        /// </summary>
        START_ADDR,

        /// <summary>
        /// sitting at data lenght prompt
        /// (Data Length is (1) 4 Bytes (2) 2 Bytes (3) 1 Byte...)
        /// </summary>
        DATA_LEN,

        /// <summary>
        /// currently at dump lenght prompt
        /// (Enter the Count to Read....(Maximun 10000))
        /// </summary>
        DUMP_COUNT,

        /// <summary>
        /// last command answered count to read prompt.
        /// read data into buffer and change state back to IDLE once we're back at the brnboot prompt
        /// </summary>
        DATA_READOUT,

        /// <summary>
        /// upload mode target address prompt
        /// </summary>
        UPLOAD_ADDR
    }
}
