namespace BRNBootDumper
{
    static class BRNBootConstants
    {
        #region common
        /// <summary>
        /// Brnboot baudrate
        /// </summary>
        public const int BRN_BAUD_RATE = 115200;

        /// <summary>
        /// the start of ram address (on my device, anyway)
        /// </summary>
        public const long START_OF_RAM = 0x80002000;

        /// <summary>
        /// the end of ram address (on my device, anyway)
        /// </summary>
        public const long END_OF_RAM = START_OF_RAM + RAM_SIZE;

        /// <summary>
        /// the size of the ram (on my device, anyway) -> 64 MiB, idk if this is acutally the right value
        /// </summary>
        public const long RAM_SIZE = 67108864; //64 MiB

        /// <summary>
        /// string to send to substitute enter keypress
        /// </summary>
        public const string BRN_ENTER = "\r";

        /// <summary>
        /// command to enter administrator mode
        /// </summary>
        public const string CMD_ADMIN_MODE = "!";

        /// <summary>
        /// pattern to match brnboot prompt (use String.endswith)
        /// [V9]:
        /// </summary>
        public const string PATTERN_PROMPT = "]:";
        #endregion

        #region dumper, cmd R
        /// <summary>
        /// command to enter "read from memory" mode
        /// </summary>
        public const string CMD_READ_MODE = "r";

        /// <summary>
        /// pattern to match "enter the start address to read: 0x" prompt (use String.contains)
        /// [V9]:
        /// </summary>
        public const string PATTERN_READ_START_ADDR = "enter the start address to read";

        /// <summary>
        /// pattern to match "data lenght is..." prompt (use String.contains)
        /// used for both memory read (r) and memory write (w)
        /// [V9]:
        /// </summary>
        public const string PATTERN_DATA_LENGHT = "data length is";

        /// <summary>
        /// pattern to match "enter the count to read..." (use String.contains)
        /// </summary>
        public const string PATTERN_READ_COUNT = "enter the count to read";
        #endregion

        #region memory upload, cmd M

        /// <summary>
        /// command to enter memory upload using XModem mode
        /// </summary>
        public const string CMD_MEMORY_UPLOAD = "m";

        /// <summary>
        /// pattern to match brnboot memory upload target address prmpt
        /// </summary>
        public const string PATTERN_MEMORY_UPLOAD_TARGET_ADDR_PROMPT = "RAM upload destination:";
        #endregion

        #region memory write, cmd W

        /// <summary>
        /// command to enter memory write mode
        /// </summary>
        public const string CMD_MEMORY_WRITE = "w";

        /// <summary>
        /// pattern to match "enter the start address to write...0x" (use String.contains)
        /// </summary>
        public const string PATTERN_WRITE_START_ADDR = "enter the start address to write";

        /// <summary>
        /// pattern to match "enter the count to write..." (use String.contains)
        /// </summary>
        public const string PATTERN_WRITE_COUNT = "enter the count to write";

        /// <summary>
        /// pattern to match "enter the data to write to the memory" (use String.contains)
        /// </summary>
        public const string PATTERN_DATA_TO_WRITE = "enter the data to write to the memory";

        /// <summary>
        /// pattern to match "Writing Process Completed" message on successfull writes (use String.contains)
        /// </summary>
        public const string PATTERN_WRITE_COMPLETED = "writing process completed";
        #endregion
    }
}
