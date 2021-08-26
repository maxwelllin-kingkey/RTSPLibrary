using System;
using System.Collections.Generic;
using System.Text;

namespace RTSPLibrary
{
    public partial class RTSPRequest
    {
        public string Method;
        public string URI;
        public RTSPHeaderSet Header = new RTSPHeaderSet();
        public int CSeq;
        public byte[] Body = null;

        public RTSPRequest(string iMethod, string iURI, int iCSeq, byte[] iBody)
        {
            InitHeaders();
            Method = iMethod;
            URI = iURI;
            CSeq = iCSeq;
            Body = iBody;
        }

        public RTSPRequest(string iMethod, string iURI, int iCSeq)
        {
            InitHeaders();
            Method = iMethod;
            URI = iURI;
            CSeq = iCSeq;
        }

        public RTSPRequest()
        {
            InitHeaders();
        }

        public override string ToString()
        {
            string RetValue;

            RetValue = Method + " " + URI + " RTSP/1.0\r\n";

            foreach (string EachKey in Header.GetKeys())
            {
                RetValue += EachKey + ": " + this.Header[EachKey] + "\r\n";
            }

            RetValue += "\r\n";

            return RetValue;
        }

        public bool Parsing(byte[] SArr)
        {
            bool RetValue = false;
            int sIndex = 0;
            int fIndex;

            while (true)
            {
                fIndex = Array.IndexOf(SArr, (byte)13, sIndex);

                if (fIndex != -1)
                {
                    if (SArr.Length >= (fIndex + 4))
                    {
                        if ((SArr[fIndex] == 13) && 
                            (SArr[fIndex + 1] == 10) && 
                            (SArr[fIndex + 2] == 13) && 
                            (SArr[fIndex + 3] == 10))
                        {
                            byte[] HeaderBytes = null;
                            string HeaderContent;
                            string[] HeaderStringList = null;

                            HeaderBytes = (byte[])Array.CreateInstance(typeof(byte), fIndex);
                            Body = (byte[])Array.CreateInstance(typeof(byte), SArr.Length - (fIndex + 4));

                            Array.Copy(SArr, 0, HeaderBytes, 0, HeaderBytes.Length);
                            Array.Copy(SArr, fIndex + 4, Body, 0, Body.Length);

                            HeaderContent = Encoding.UTF8.GetString(HeaderBytes);
                            HeaderStringList = HeaderContent.Split("\r\n");

                            if (HeaderStringList.Length > 0)
                            {
                                int TmpIndex;
                                string ProtocolHeader = HeaderStringList[0];

                                TmpIndex = ProtocolHeader.IndexOf(" ");
                                if (TmpIndex != -1)
                                {
                                    Method = ProtocolHeader.Substring(0, TmpIndex).Trim();
                                    ProtocolHeader = ProtocolHeader.Substring(TmpIndex + 1).Trim();

                                    TmpIndex = ProtocolHeader.IndexOf(" ");
                                    if (TmpIndex != -1)
                                        URI = ProtocolHeader.Substring(0, TmpIndex).Trim();
                                    else
                                        URI = ProtocolHeader;

                                    for (int I = 1; I < HeaderStringList.Length; I++)
                                    {
                                        string EachStr = HeaderStringList[I];

                                        if ( string.IsNullOrWhiteSpace(EachStr) == false)
                                        {
                                            string TmpCmd;
                                            string TmpValue;

                                            TmpIndex = EachStr.IndexOf(":");
                                            if (TmpIndex != -1)
                                            {
                                                TmpCmd = EachStr.Substring(0, TmpIndex).Trim();
                                                TmpValue = EachStr.Substring(TmpIndex + 1).Trim();

                                                if (TmpCmd.ToUpper() == "CSeq".ToUpper())
                                                    CSeq = Convert.ToInt32(TmpValue);
                                                else
                                                    Header.Set(TmpCmd, TmpValue);
                                            }
                                            else
                                                Header.Set(EachStr.Trim(), string.Empty);
                                        }
                                    }

                                    RetValue = true;

                                    break;
                                }
                                else
                                    // protocol error
                                    break;
                            }
                            else
                                // no header
                                break;
                        }
                        else
                            sIndex = fIndex + 1;
                    }
                    else
                        break;
                }
                else
                    break;
            }
            
            return RetValue;
        }

        public byte[] ToByteArray(bool EncodingBase64)
        {
            List<byte> iContentList = new List<byte>();

            iContentList.AddRange(Encoding.UTF8.GetBytes(Method + " " + URI + " RTSP/1.0\r\n"));

            if (Body != null)
            {
                if (Body.Length > 0)
                    Header.Set("Content-Length", Body.Length.ToString());
            }

            iContentList.AddRange(Encoding.UTF8.GetBytes("CSeq: " + CSeq + "\r\n"));

            foreach (string EachKey in Header.GetKeys())
            {
                string EachValue = this.Header[EachKey];

                iContentList.AddRange(Encoding.UTF8.GetBytes(EachKey + ": " + EachValue + "\r\n"));
            }

            iContentList.AddRange(new byte[] { 13, 10 });
            if (Body != null)
            {
                if (Body.Length > 0)
                    iContentList.AddRange(Body);
            }

            if (EncodingBase64)
                return Encoding.UTF8.GetBytes(Convert.ToBase64String(iContentList.ToArray()));
            else
                return iContentList.ToArray();
        }

        private void InitHeaders()
        {
            Header.Set("User-Agent", "KingKey-RTSP Module");
        }
    }
}