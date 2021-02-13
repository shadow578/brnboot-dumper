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
        public const string PATTERN_START_ADDR = "enter the start address to read";

        /// <summary>
        /// pattern to match "data lenght is..." prompt (use String.contains)
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

        /// <summary>
        /// the default target address for memory upload (start of ram)
        /// </summary>
        public const long MEMORY_UPLOAD_DEFAULT_ADDR = 0x80002000;
        #endregion
    }
}
