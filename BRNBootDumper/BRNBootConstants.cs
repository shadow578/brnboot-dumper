namespace BRNBootDumper
{
    static class BRNBootConstants
    {
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
        /// command to enter "read from memory" mode
        /// </summary>
        public const string CMD_READ_MODE = "r";

        /// <summary>
        /// pattern to match brnboot prompt (use String.endswith)
        /// [V9]:
        /// </summary>
        public const string PATTERN_PROMPT = "]:";

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

    }
}
