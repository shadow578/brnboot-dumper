namespace BRNBootDumper
{
    public static class XModemConstants
    {
        public const int MODEM_DATA_SIZE = 128;

        public const int MODEM_PACKET_SIZE = MODEM_DATA_SIZE + 4;

        public const char MODEM_SOH = (char)0x01;

        public const char MODEM_EOT = (char)0x04;

        public const char MODEM_ACK = (char)0x06;

        public const char MODEM_NAK = (char)0x15;

        public const byte MODEM_PADDING = 0x1a;

        public const char MODEM_WAIT_FOR_CONNECTION = 'C';



    }
}
