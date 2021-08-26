using System;
using System.Linq;

namespace RTSPLibrary
{
    public partial class RTP
    {
        private UltimateByteArrayClass iData;
        private int iOffset;
        private int iDataLength;

        public UltimateByteArrayClass InternalArray
        {
            get { return iData; }
        }

        public int Version
        {
            get { return (this.iData[iOffset + 0] & 0xc0) >> 6; }
            set { this.iData[iOffset + 0] = (byte)((this.iData[iOffset + 0] & 0xc0) | ((byte)value & 0x3 << 6)); }
        }

        public int PaddingCount
        {
            get
            {
                int RetValue;

                if (Padding)
                    RetValue = this.iData[iOffset + (iDataLength - 1)];
                else
                    RetValue = 0;

                return RetValue;
            }
        }

        public bool Padding
        {
            get{ return ((this.iData[iOffset + 0] & 0x20) != 0) ? true : false; }

            set
            {
                if (value)
                    this.iData[iOffset + 0] = (byte)(this.iData[iOffset + 0] | 0x20);
                else
                    this.iData[iOffset + 0] = (byte)(this.iData[iOffset + 0] & 0xdf);
            }
        }

        public bool Extension
        {
            get { return ((this.iData[iOffset + 0] & 0x10) != 0) ? true : false; }
        }

        public int ExtensionProfile
        {
            get
            {
                if (Extension)
                    return this.iData[iOffset + 12] * 256 + this.iData[iOffset + 13];
                else
                    return 0;
            }
        }

        public int ExtensionLength
        {
            get
            {
                if (Extension)
                    return this.iData[iOffset + 14] * 256 + this.iData[iOffset + 15];
                else
                    return 0;
            }
        }

        public int CSRCount
        {
            get
            {
                return this.iData[iOffset + 0] & 0xf;
            }
        }

        public bool Marker
        {
            get
            {
                return ((this.iData[iOffset + 1] & 0x80) != 0) ? true : false;
            }

            set
            {
                if (value)
                    this.iData[iOffset + 1] = (byte)(this.iData[iOffset + 1] | 0x80);
                else
                    this.iData[iOffset + 1] = (byte)(this.iData[iOffset + 1] & 0x7f);
            }
        }

        public int PayloadType
        {
            get { return this.iData[iOffset + 1] & 0x7f; }
            set { this.iData[iOffset + 1] = (byte)(this.iData[iOffset + 1] | (value & 0x7f)); }
        }

        public ushort SequenceNumber
        {
            get { return (ushort)(this.iData[iOffset + 2] * 256 + this.iData[iOffset + 3]); }

            set
            {
                this.iData[iOffset + 2] = (byte)(value / 256);
                this.iData[iOffset + 3] = (byte)(value % 256);
            }
        }

        public uint Timestamp
        {
            get
            {
                return (uint)(this.iData[iOffset + 4] * Math.Pow(256, 3) + 
                              this.iData[iOffset + 5] * Math.Pow(256, 2) + 
                              this.iData[iOffset + 6] * 256 + 
                              this.iData[iOffset + 7]);
            }

            set
            {
                for (int I = 0; I <= 3; I++)
                    this.iData[iOffset + 4 + I] = (byte)(Math.Round(value % Math.Pow(256d, 3 - I + 1)) / Math.Round(Math.Pow(256d, 3 - I)));
            }
        }

        public uint SSRC
        {
            get
            {
                return BitConverter.ToUInt32(new byte[] { this.iData[iOffset + 8], this.iData[iOffset + 9], this.iData[iOffset + 10], this.iData[iOffset + 11] }, 0);
            }

            set
            {
                byte[] SSRCBytes = null;

                SSRCBytes = BitConverter.GetBytes(value);

                this.iData[iOffset + 8] = SSRCBytes[0];
                this.iData[iOffset + 9] = SSRCBytes[1];
                this.iData[iOffset + 10] = SSRCBytes[2];
                this.iData[iOffset + 11] = SSRCBytes[3];
            }
        }

        public byte[] GetPadding
        {
            get
            {
                byte[] Padding = null;
                int iPaddingLength;

                if (this.Padding)
                {
                    iPaddingLength = PaddingCount;

                    if (iPaddingLength > 0)
                    {
                        if (iData.Count >= iPaddingLength)
                        {
                            Padding = (byte[])Array.CreateInstance(typeof(byte), iPaddingLength - 1);
                            iData.CopyTo(iOffset + (iDataLength - iPaddingLength), Padding, 0, Padding.Length);
                        }
                    }
                }

                return Padding;
            }
        }

        public byte[] GetExtension
        {
            get
            {
                byte[] ExtBody = null;

                if (Extension)
                {
                    int iHeaderLength = 16 + CSRCount * 4;

                    if (ExtensionLength < 250000)
                    {
                        if ((iData.Count - iHeaderLength) >= (ExtensionLength * 4))
                        {
                            ExtBody = (byte[])Array.CreateInstance(typeof(byte), ExtensionLength * 4);
                            iData.CopyTo(iOffset + iHeaderLength, ExtBody, 0, ExtBody.Length);
                        }
                    }
                }

                return ExtBody;
            }
        }

        public int CopyPayloadTo(byte[] DestArray, int DestOffset)
        {
            int payloadOffset = GetPayloadOffset;
            int payloadLength = GetPayloadBodyLength;
            int copyLength = 0;

            if ((iData.Count - GetPayloadOffset) >= payloadLength)
            {
                if ((DestArray.Length - DestOffset) >= payloadLength)
                {
                    iData.CopyTo(payloadOffset, DestArray, DestOffset, payloadLength);
                    copyLength = payloadLength;
                }
            }

            return copyLength;
        }

        public int CopyPayloadTo(UltimateByteArrayClass DestArray)
        {
            int payloadOffset = GetPayloadOffset;
            int payloadLength = GetPayloadBodyLength;
            int copyLength = 0;

            if ((iData.Count - GetPayloadOffset) >= payloadLength)
            {
                DestArray.AddRange(iData, payloadOffset, payloadLength);
                copyLength = payloadLength;
            }

            return copyLength;
        }

        public byte[] GetPayload
        {
            get
            {
                byte[] Payload = null;
                int payloadOffset = GetPayloadOffset;
                int payloadLength = GetPayloadBodyLength;

                if ((iData.Count - GetPayloadOffset) >= payloadLength)
                {
                    Payload = (byte[])Array.CreateInstance(typeof(byte), payloadLength);
                    iData.CopyTo(GetPayloadOffset, Payload, 0, Payload.Length);
                }

                return Payload;
            }
        }

        public int GetPayloadOffset
        {
            get { return iOffset + GetHeaderLength; }
        }

        public int GetPayloadBodyLength
        {
            get
            {
                int iHeaderLength = GetHeaderLength;
                int iPaddingLength = 0;
                int BodyLength = 0;

                if (Padding)
                {
                    iPaddingLength = PaddingCount;
                    BodyLength = iDataLength - iHeaderLength - iPaddingLength;
                }
                else
                {
                    BodyLength = iDataLength - iHeaderLength;
                }

                if (BodyLength >= 0)
                {
                    if (BodyLength > (iData.Count - iHeaderLength))
                        BodyLength = iData.Count - iHeaderLength;
                }
                else
                {
                    BodyLength = 0;
                }

                return BodyLength;
            }
        }

        public byte[] ToByteArray()
        {
            if ((iData.Count == iDataLength) && (iOffset == 0))
            {
                return iData.ToArray();
            }
            else
            {
                byte[] RetBytes = null;

                RetBytes = (byte[])Array.CreateInstance(typeof(byte), iDataLength);
                iData.CopyTo(iOffset, RetBytes, 0, iDataLength);

                return RetBytes;
            }
        }

        public RTP(byte[] RTPPacket)
        {
            iData = new UltimateByteArrayClass(2000000);
            iData.AddRange(RTPPacket);

            iOffset = 0;
            iDataLength = iData.Count;
        }

        public RTP(byte[] RTPPacket, int OffsetIndex, int RTPPacketLength)
        {
            iData = new UltimateByteArrayClass(2000000);
            iData.AddRange(RTPPacket);

            iOffset = OffsetIndex;
            iDataLength = RTPPacketLength;
        }

        public RTP(UltimateByteArrayClass RTPPacket)
        {
            iData = RTPPacket;
            iOffset = 0;
            iDataLength = iData.Count;
        }

        public RTP(UltimateByteArrayClass RTPPacket, int OffsetIndex, int RTPPacketLength)
        {
            iData = RTPPacket;
            iOffset = OffsetIndex;
            iDataLength = RTPPacketLength;
        }

        public RTP(byte[] Payload, byte[] Padding, int PayloadType, int SeqNumber, uint Timestamp)
        {
            int iHeaderLength;
            bool HasPadding = false;

            iOffset = 0;
            iHeaderLength = 12;
            if (Padding != null)
            {
                if (Padding.Length > 0)
                    HasPadding = true;
            }

            if (iData == null)
                iData = new UltimateByteArrayClass(2000000);

            if (HasPadding)
            {
                iDataLength = Payload.Length + iHeaderLength + Padding.Length + 1;
                iData.AddRange((byte[])Array.CreateInstance(typeof(byte), iHeaderLength));
                iData.AddRange(Payload);
                iData.AddRange(Padding);
                iData.AddRange(new byte[] { (byte)(Padding.Length + 1) });
            }
            else
            {
                iDataLength = Payload.Length + iHeaderLength;
                iData.AddRange((byte[])Array.CreateInstance(typeof(byte), iHeaderLength));
                iData.AddRange(Payload);
            }

            Version = 2;

            this.PayloadType = PayloadType;
            SequenceNumber = (ushort)SeqNumber;
            this.Timestamp = Timestamp;
            this.Padding = HasPadding;
        }

        internal int GetHeaderLength
        {
            get
            {
                int iHeaderLength = 0;

                if (Extension)
                    iHeaderLength = ExtensionLength * 4 + 16 + CSRCount * 4;
                else
                    iHeaderLength = 12 + CSRCount * 4;

                return iHeaderLength;
            }
        }
    }
}