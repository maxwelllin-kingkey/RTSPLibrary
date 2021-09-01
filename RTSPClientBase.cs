using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace RTSPLibrary
{
    public partial class RTSPClientBase : IDisposable
    {
        private System.Net.Sockets.TcpClient iTCP;
        private int iCSeq = 1;
        private BufferClass BC = new BufferClass();
        private bool iCvtBase64Send = false;
        private DateTime iMagicLastSendTime;
        private int iRecvServerChannel;
        private int iLastMagicDataLength;

        public const string DESCRIBE_accept_type_sdp = "application/sdp";
        public const string DESCRIBE_accept_type_rtsl = "application/rtsl";
        public const string DESCRIBE_accept_type_mheg = "application/mheg";
        public const string Method_DESCRIBE = "DESCRIBE";
        public const string Method_SETUP = "SETUP";
        public const string Method_PLAY = "PLAY";
        public const string Method_OPTIONS = "OPTIONS";
        public const string Method_ANNOUNCE = "ANNOUNCE";
        public const string Method_PAUSE = "PAUSE";
        public const string Method_TEARDOWN = "TEARDOWN";
        public const string Method_GET_PARAMETER = "GET_PARAMETER";
        public const string Method_SET_PARAMETER = "SET_PARAMETER";
        public const string Method_REDIRECT = "REDIRECT";
        public const string Method_RECORD = "RECORD";

        public delegate void DisconnectEventHandler(RTSPClientBase sender);
        public event DisconnectEventHandler Disconnect;

        private HttpSpliter HS = new HttpSpliter();

        public bool CvtBase64Send
        {
            get { return iCvtBase64Send; }
            set { iCvtBase64Send = value; }
        }

        public System.Net.Sockets.TcpClient GetSourceTCP
        {
            get { return iTCP; }
        }

        public byte[] GetAndFlushBuffer()
        {
            byte[] RetValue = null;

            lock (BC.iBuffer)
            {
                if (BC.iBuffer.Count > 0)
                {
                    RetValue = BC.iBuffer.ToArray();
                    BC.iBuffer.Clear();
                }
            }

            return RetValue;
        }

        public RTSPRequest CreateRequest(string Method, string URI)
        {
            RTSPRequest RetValue = null;

            RetValue = new RTSPRequest(Method, URI, GetCSeq());

            return RetValue;
        }

        public void CloseServer()
        {
            if (iTCP != null)
            {
                System.Net.Sockets.NetworkStream netStream = null;
                System.Net.IPEndPoint IPEP = null;
                string RemoteIP = null;
                int RemotePort = 0;

                try { IPEP = (System.Net.IPEndPoint)iTCP.Client.RemoteEndPoint; }
                catch (Exception ex) { }

                if (IPEP != null)
                {
                    RemoteIP = IPEP.Address.ToString();
                    RemotePort = IPEP.Port;
                }

                if (iTCP.Connected)
                {
                    try { netStream = iTCP.GetStream(); }
                    catch (Exception ex) { }

                    if (netStream != null)
                    {
                        try { netStream.Close(); }
                        catch (Exception ex) { }
                    }
                }

                try { iTCP.Close(); }
                catch (Exception ex) { }

                Console.WriteLine("RTSPClientBase Socket Close completed [" + RemoteIP + ":" + RemotePort + "]");
                iTCP = null;
            }
        }

        public void ConnectServer(string RemoteAddr, int RemotePort)
        {
            if (iTCP != null)
                CloseServer();

            iTCP = new System.Net.Sockets.TcpClient();
            iTCP.NoDelay = true;

            try { iTCP.Connect(RemoteAddr, RemotePort); }
            catch (Exception ex)
            {
                try { iTCP.Close(); }
                catch (Exception ex2) { }

                iTCP = null;

                throw ex;
            }
        }

        public RTSPResponse SendAndWait(RTSPRequest RTSPReq)
        {
            bool SendSuccess = false;
            RTSPResponse Resp = null;

            SendSuccess = WriteRequest(RTSPReq);
            if (SendSuccess)
                Resp = WaitResponse(10000);

            return Resp;
        }

        public bool WriteRequest(RTSPRequest Req)
        {
            byte[] DataArray = null;
            List<byte> ReadBufferList = new List<byte>();
            bool SendSuccess = false;

            DataArray = Req.ToByteArray(iCvtBase64Send);
            try
            {
                if (iTCP != null)
                {
                    if (iTCP.Connected)
                    {
                        iTCP.GetStream().Write(DataArray, 0, DataArray.Length);
                        SendSuccess = true;
                    }
                }
            }
            catch (Exception ex)
            {
            }

            return SendSuccess;
        }

        public RTSPResponse WaitResponse(int WaitTimeout)
        {
            byte[] DataArray = null;
            byte[] ReadBuffer = null;
            List<byte> ReadBufferList = new List<byte>();
            int ReadCount;
            bool SendSuccess = false;
            RTSPResponse Resp = null;
            bool MustExit = false;

            ReadBuffer = (byte[])Array.CreateInstance(typeof(byte), 1024);
            iTCP.ReceiveTimeout = WaitTimeout;

            while (true)
            {
                ReadCount = 0;

                try
                {
                    if (iTCP != null)
                    {
                        if (iTCP.Connected)
                            ReadCount = iTCP.GetStream().Read(ReadBuffer, 0, ReadBuffer.Length);
                    }
                }
                catch (Exception ex)
                {
                    BC.Clear();
                    Debug.WriteLine("TimeOut:" + ex.Message);
                }

                if (ReadCount > 0)
                {
                    DataArray = (byte[])Array.CreateInstance(typeof(byte), ReadCount);
                    Array.Copy(ReadBuffer, 0, DataArray, 0, DataArray.Length);

                    BC.iBuffer.AddRange(DataArray);
                }
                else
                {
                    MustExit = true;
                }

                Resp = GetResponse();
                if ((Resp != null) || (MustExit))
                    break;
            }

            return Resp;
        }

        private RTSPResponse GetResponse()
        {
            RTSPResponse RetValue = null;
            int sIndex = 0;
            int fIndex;
            int LoopIndex1;
            int LoopIndex2;

            for (LoopIndex1 = 1; LoopIndex1 <= 100; LoopIndex1++)
            {
                if (BC.iHeaderCapture == false)
                {
                    for (LoopIndex2 = 1; LoopIndex2 <= 100; LoopIndex2++)
                    {
                        fIndex = BC.iBuffer.IndexOf(13, sIndex);
                        if (fIndex != -1)
                        {
                            if (BC.iBuffer.Count >= (fIndex + 4))
                            {
                                if ((BC.iBuffer[fIndex] == 13) &&
                                    (BC.iBuffer[fIndex + 1] == 10) &&
                                    (BC.iBuffer[fIndex + 2] == 13) &&
                                    (BC.iBuffer[fIndex + 3] == 10))
                                {
                                    byte[] HeaderBytes = null;
                                    string HeaderContent;
                                    string[] HeaderStringList = null;

                                    HeaderBytes = (byte[])Array.CreateInstance(typeof(byte), fIndex);
                                    BC.iBuffer.CopyTo(0, HeaderBytes, 0, HeaderBytes.Length);

                                    HeaderContent = Encoding.UTF8.GetString(HeaderBytes);

                                    HeaderStringList = HS.SplitContent(HeaderContent);
                                    if (HeaderStringList.Length > 0)
                                    {
                                        int TmpIndex;

                                        for (int I = 1; I < HeaderStringList.Length; I++)
                                        {
                                            string EachStr = HeaderStringList[I];

                                            if (string.IsNullOrWhiteSpace(EachStr.Trim()) == false)
                                            {
                                                string TmpCmd;
                                                string TmpValue;

                                                TmpIndex = EachStr.IndexOf(":");
                                                if (TmpIndex != -1)
                                                {
                                                    TmpCmd = EachStr.Substring(0, TmpIndex).Trim();
                                                    TmpValue = EachStr.Substring(TmpIndex + 1).Trim();

                                                    if (TmpCmd.ToUpper() == "Content-Length".ToUpper())
                                                    {
                                                        BC.iContentLength = Convert.ToInt32(TmpValue);
                                                        break;
                                                    }
                                                }
                                            }
                                        }

                                        BC.iHeaderCapture = true;
                                        BC.iHeaderLength = fIndex + 4;

                                        break;
                                    }
                                    else
                                        // header = 0 
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

                    if (LoopIndex2 >= 100)
                    {
                        BC.iBuffer.Clear();
                        BC.Reset();
                    }

                    if (BC.iHeaderCapture == false)
                        break;
                }
                else if (BC.iContentLength != -1)
                {
                    if (BC.iBuffer.Count >= BC.iHeaderLength + BC.iContentLength)
                    {
                        byte[] Content = null;

                        Content = (byte[])Array.CreateInstance(typeof(byte), BC.iHeaderLength + BC.iContentLength);

                        BC.iBuffer.CopyTo(0, Content, 0, Content.Length);
                        BC.iBuffer.RemoveRange(0, Content.Length);

                        RetValue = new RTSPResponse();
                        RetValue.Parsing(Content);

                        BC.Reset();

                        break;
                    }
                    else
                        break;
                }
                else
                {
                    // 未設定 Content-Length
                    // 代表沒有內容, Header 收完即可
                    // Dim Header() As Byte

                    // Header = Array.CreateInstance(GetType(Byte), BC.iHeaderLength)
                    // BC.iBuffer.CopyTo(0, Header, 0, Header.Length)
                    // BC.iBuffer.RemoveRange(0, Header.Length)

                    // RetValue = New RTSPResponse
                    // RetValue.Parsing(Header)

                    RetValue = new RTSPResponse();

                    RetValue.Parsing(BC.iBuffer.ToArray());

                    BC.iBuffer.Clear();
                    BC.Reset();

                    break;
                }
            }

            if (LoopIndex1 >= 100)
            {
                BC.iBuffer.Clear();
                BC.Reset();
            }

            return RetValue;
        }

        public List<byte> GetBuffer()
        {
            return BC.iBuffer;
        }

        private int GetCSeq()
        {
            return System.Threading.Interlocked.Increment(ref iCSeq);
        }

        public partial class BufferClass
        {
            public List<byte> iBuffer = new List<byte>();
            public bool iHeaderCapture = false;
            public int iHeaderLength = -1;
            public int iContentLength = -1;

            public void Reset()
            {
                iHeaderCapture = false;
                iContentLength = -1;
                iHeaderLength = -1;
            }

            public void Clear()
            {
                Reset();
                iBuffer.Clear();
            }
        }

        #region Dispose
        private bool disposedValue = false;

        // IDisposable
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (iTCP != null)
                    {
                        try { iTCP.Close(); }
                        catch (Exception ex) { }

                        iTCP = null;
                    }
                }
            }

            disposedValue = true;
        }

        #region  IDisposable Support 
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
        #endregion
    }
}