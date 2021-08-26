namespace RTSPLibrary
{
    public partial class HttpPushFrame
    {
        public RTSPHeaderSet Header;
        public UltimateByteArrayClass Body;
        public int BodyOffset;
        public int BodyLength;
    }
}