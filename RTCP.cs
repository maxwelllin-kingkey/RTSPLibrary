using System;

namespace RTSPLibrary
{
    public partial class RTCP
    {
        private byte[] iData = null;

        public enum enumPacketType
        {
            FIR = 192,
            NACK = 193,
            SenderReport = 200,
            ReceiverReport = 201,
            SDES = 202,
            Bye = 203,
            APP = 204,
            RTPFeedback = 205,
            PayloadSpecific = 206,
            XR = 207
        }

        public int HeaderLength
        {
            get { return 4; }
        }

        public int Version
        {
            get { return (iData[0] & 0xC0) >> 6; }
            set { iData[0] = (byte)((iData[0] & 0xC0) | ((byte)value << 6)); }
        }

        public bool IsPadding
        {
            get { return ((iData[0] & 0x20) == 0) ? false : true; }
        }

        public int ReportCount
        {
            get { return iData[0] & 0x1f; }
            set { iData[0] = (byte)((iData[0] & 0xe0) | ((byte)value & 0x1f)); }
        }

        public enumPacketType PacketType
        {
            get { return (enumPacketType)iData[1]; }
            set { iData[1] = (byte)value; }
        }

        public RTCPBody GetBody()
        {
            switch (PacketType)
            {
                case enumPacketType.ReceiverReport:
                    return new RTCPReceiverReport(iData, 4);
                case enumPacketType.SenderReport:
                    return new RTCPSenderReport(iData, 4);
                default:
                    return new RTCPBody(iData, 4);
            }
        }

        public int TotalLength
        {
            get { return (iData[2] * 256 + iData[3]) * 4; }
            set 
            {
                // 必須為 4 的倍數
                if (value % 4 == 0)
                {
                    iData[2] = (byte)((long)Math.Round(value / 4d) / 256L);
                    iData[3] = (byte)Math.Round(value / 4d % 256d);
                }
                else
                {
                    throw new Exception("must 4");
                }
            }
        }

        public RTCP(byte[] RTCPPacket)
        {
            iData = RTCPPacket;
        }

        public partial class RTCPBody
        {
            protected byte[] iBody = null;
            protected int iBodyStartIndex;

            public virtual byte[] ToByteArray()
            {
                byte[] RetValue = null;

                RetValue = (byte[])Array.CreateInstance(typeof(byte), iBody.Length - iBodyStartIndex);

                Array.Copy(iBody, iBodyStartIndex, RetValue, 0, RetValue.Length);

                return RetValue;
            }

            public RTCPBody(byte[] Body, int BodyStartIndex)
            {
                iBody = Body;
                iBodyStartIndex = BodyStartIndex;
            }
        }

        public partial class RTCPSenderReport : RTCPBody
        {
            public uint SSRC
            {
                get
                {
                    return (uint)Math.Round(iBody[iBodyStartIndex] * Math.Pow(256, 3) + 
                                            iBody[iBodyStartIndex + 1] * Math.Pow(256, 2) + 
                                            iBody[iBodyStartIndex + 2] * 256 + 
                                            iBody[iBodyStartIndex + 3]);
                }

                set
                {
                    for (int I = 0; I <= 3; I++)
                        iBody[iBodyStartIndex + 0 + I] = (byte)((long)Math.Round(value % Math.Pow(256d, 3 - I + 1)) / (long)Math.Round(Math.Pow(256d, 3 - I)));
                }
            }

            public byte[] TimestampMSWBytes
            {
                get
                {
                    byte[] RetValue = (byte[])Array.CreateInstance(typeof(byte), 4);

                    Array.Copy(iBody, iBodyStartIndex + 4, RetValue, 0, 4);

                    return RetValue;
                }
            }

            public byte[] TimestampLSWBytes
            {
                get
                {
                    byte[] RetValue = (byte[])Array.CreateInstance(typeof(byte), 4);

                    Array.Copy(iBody, iBodyStartIndex + 8, RetValue, 0, 4);

                    return RetValue;
                }
            }

            public uint TimestampMSW
            {
                get
                {
                    return (uint)Math.Round(iBody[iBodyStartIndex + 4] * Math.Pow(256d, 3d) + 
                                            iBody[iBodyStartIndex + 5] * Math.Pow(256d, 2d) + 
                                            iBody[iBodyStartIndex + 6] * 256 + 
                                            iBody[iBodyStartIndex + 7]);
                }

                set
                {
                    for (int I = 0; I <= 3; I++)
                        iBody[iBodyStartIndex + 4 + I] = (byte)((long)Math.Round(value % Math.Pow(256d, 3 - I + 1)) / (long)Math.Round(Math.Pow(256d, 3 - I)));
                }
            }

            public uint TimestampLSW
            {
                get
                {
                    return (uint)Math.Round(iBody[iBodyStartIndex + 8] * Math.Pow(256d, 3d) + 
                                            iBody[iBodyStartIndex + 9] * Math.Pow(256d, 2d) + 
                                            iBody[iBodyStartIndex + 10] * 256 + 
                                            iBody[iBodyStartIndex + 11]);
                }

                set
                {
                    for (int I = 0; I <= 3; I++)
                        iBody[iBodyStartIndex + 8 + I] = (byte)((long)Math.Round(value % Math.Pow(256d, 3 - I + 1)) / (long)Math.Round(Math.Pow(256d, 3 - I)));
                }
            }

            public uint RTPTimestamp
            {
                get
                {
                    return (uint)Math.Round(iBody[iBodyStartIndex + 12] * Math.Pow(256d, 3d) + 
                                            iBody[iBodyStartIndex + 13] * Math.Pow(256d, 2d) + 
                                            iBody[iBodyStartIndex + 14] * 256 + 
                                            iBody[iBodyStartIndex + 15]);
                }

                set
                {
                    for (int I = 0; I <= 3; I++)
                        iBody[iBodyStartIndex + 12 + I] = (byte)((long)Math.Round(value % Math.Pow(256d, 3 - I + 1)) / (long)Math.Round(Math.Pow(256d, 3 - I)));
                }
            }

            public uint SenderPacketCount
            {
                get
                {
                    return (uint)Math.Round(iBody[iBodyStartIndex + 16] * Math.Pow(256d, 3d) + 
                                            iBody[iBodyStartIndex + 17] * Math.Pow(256d, 2d) + 
                                            iBody[iBodyStartIndex + 18] * 256 + 
                                            iBody[iBodyStartIndex + 19]);
                }

                set
                {
                    for (int I = 0; I <= 3; I++)
                        iBody[iBodyStartIndex + 16 + I] = (byte)((long)Math.Round(value % Math.Pow(256d, 3 - I + 1)) / (long)Math.Round(Math.Pow(256d, 3 - I)));
                }
            }

            public uint SenderOctetCount
            {
                get
                {
                    return (uint)Math.Round(iBody[iBodyStartIndex + 20] * Math.Pow(256d, 3d) + 
                                            iBody[iBodyStartIndex + 21] * Math.Pow(256d, 2d) + 
                                            iBody[iBodyStartIndex + 22] * 256 + 
                                            iBody[iBodyStartIndex + 23]);
                }

                set
                {
                    for (int I = 0; I <= 3; I++)
                        iBody[iBodyStartIndex + 20 + I] = (byte)((long)Math.Round(value % Math.Pow(256d, 3 - I + 1)) / (long)Math.Round(Math.Pow(256d, 3 - I)));
                }
            }

            public RTCPSenderReport(byte[] Body, int BodyStartIndex) : base(Body, BodyStartIndex)
            {
            }
        }

        public partial class RTCPReceiverReport : RTCPBody
        {
            public uint SSRC
            {
                get { return BitConverter.ToUInt32(iBody, iBodyStartIndex); }
                set { Array.Copy(BitConverter.GetBytes(value), 0, iBody, iBodyStartIndex, 4); }
            }

            public RTCPReceiverReport(byte[] Body, int BodyStartIndex) : base(Body, BodyStartIndex)
            {
            }
        }
    }
}