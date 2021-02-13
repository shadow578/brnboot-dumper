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
        /// sitting at start address prompt of read mode
        /// (Enter the Start Address to Read....0x)
        /// </summary>
        DUMP_START_ADDR,

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
        DATAx_READOUT,

        /// <summary>
        /// upload mode target address prompt
        /// </summary>
        UPLOAD_ADDR,

        /// <summary>
        /// currently at start address prompt of write mode
        /// (Enter the Start Address to WRITE...0x)
        /// </summary>
        WRITE_START_ADDR,

        /// <summary>
        /// currently at write count prompt
        /// (Enter the Count to Write..)
        /// </summary>
        WRITE_COUNT,

        /// <summary>
        /// currently at data entry prompt
        /// (Enter the Data to Write to the Memory...0x)
        /// </summary>
        WRITE_DATA_PROMPT,

        /// <summary>
        /// write completed, sitting at prompt right after this message (== equal to IDLE with some delay :P)
        /// (Writing Process Completed
        /// 
        /// [VR9 Boot]: )
        /// </summary>
        WRITE_COMPLETED

    }
}
