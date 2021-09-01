using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace RTSPLibrary
{
    public class RTSPClient : IDisposable
    {
        private int iSec = 0;

        private RTSPClientBase iBaseRecv;
        private RTSPClientBase iBaseSender;

        private RTSPAuthorization iRTSPAuth;
        private List<RTSPClientStream> iStreamList = new List<RTSPClientStream>();

        private Dictionary<int, RTSPClientStream> iRegStreamPort = new Dictionary<int, RTSPClientStream>();
        private Dictionary<int, RTSPClientStream> iRegControlPort = new Dictionary<int, RTSPClientStream>();

        private int iBasePortIndex = 0;
        private UltimateByteArrayClass iFrameRawData = new UltimateByteArrayClass(2000000);
        private int iSendOutFrameSequenceNumber = 0;
        // Private iFrameRawDataSize As Integer = 2000000 ' 2M

        private string iURL;
        private string iConnectedRemoteIP;
        private int iConnectedRemotePort;
        private bool iConnectionClose = false;
        private bool iHttpHeaderSended = false;

        private bool iInReceiveProcess = false;

        private enumProtocolType iProtocolType = enumProtocolType.RTPOverTCP;
        private bool iHostURI = true;  // 要求 RTSP URI 包含 HOST 資訊
        private string iHttpContentType = string.Empty;
        private string iHttpConnectionType = "Keep-Alive";
        private string iHttpMethod = "GET";
        private string iUserAgent = "Kingkey RTSP Module";
        private int xSession;
        private string iSessionID = string.Empty;
        private bool iIsNeedAuth = false;
        private bool iSendAuthFirst = false;
        private HashSet<byte> iBoundaryHash = new HashSet<byte>();
        private byte[] iHttpBoundary = null;
        private int iLastBoundarySearchIndex = 0;
        private bool iWaitingBoundary = false;
        private int iWaitContentLength = 0;
        private RTSPHeaderSet iBoundaryHeader;
        private enumRTPLostSearch iRTPLostSearch = enumRTPLostSearch.SearchNext;

        private int iBufferSize = 8192;
        private byte[] iBuffer = null;

        private int iRTPSeq;
        private bool iRTCPServerReport = false;
        private enumRTCPControlType iRTCPControlType = enumRTCPControlType.Auto;
        private RTP iLastReceivedRTP;

        private enumStatus iStatus = enumStatus.None;

        private int iLastDataLength;
        private long iLastSeqNumber;
        private bool iIsBeginReceived = false;
        private RTSPRequest iLastRC = null/* TODO Change to default(_) if this is not a reference type */;
        // Private iLastSenderReport As RTCP.RTCPSenderReport

        private System.Threading.Thread iRTCPLoopThread = null;
        private bool iRTCPLoopExit = false;

        public object Tag;

        public enum enumRTPLostSearch
        {
            SearchNext,
            WaitNext
        }

        public enum enumRTCPControlType
        {
            Auto = 0,
            Always = 1,
            Disable = 2
        }

        public enum enumStatus
        {
            None = 0,
            OPTIONS = 1,
            DESCRIBE = 2,
            SETUP = 3,
            PLAY = 4,
            Ready = 5,
            TEARDOWN = 6,
            Closing = 7
        }

        public enum enumProtocolType
        {
            RTPOverTCP,
            RTPOverUDP,
            RTSPOverHTTP_B64,
            RTSPOverHTTP_Text,
            RTSPOverHTTP_B64_Type2,
            RTSPOverHTTP_Text_Type2,
            HttpPush,
            HttpRaw
        }

        public delegate void RTSPCmdSendBeforeEventHandler(RTSPClient sender, RTSPRequest RC);
        public event RTSPCmdSendBeforeEventHandler RTSPCmdSendBefore;

        public delegate void RTSPCmdSendResponseEventHandler(RTSPClient sender, RTSPRequest RC, RTSPResponse RP);
        public event RTSPCmdSendResponseEventHandler RTSPCmdSendResponse;

        public delegate void RTSPDisconnectEventHandler(RTSPClient sender);
        public event RTSPDisconnectEventHandler RTSPDisconnect;

        public delegate void RTPStreamFrameEventHandler(RTSPClient sender, int StreamPort, RTP RTPFrame);
        public event RTPStreamFrameEventHandler RTPStreamFrame;

        public delegate void RTPControlFrameEventHandler(RTSPClient sender, int ControlPort, RTCP[] RTCPFrame);
        public event RTPControlFrameEventHandler RTPControlFrame;

        public delegate void HttpPushFrameEventHandler(RTSPClient sender, HttpPushFrame PushFrame);
        public event HttpPushFrameEventHandler HttpPushFrame;


        public string HttpContentType
        {
            get { return iHttpContentType; }
            set { iHttpContentType = value; }
        }

        public string HttpConnectionType
        {
            get { return iHttpConnectionType; }
            set
            {
                if (string.IsNullOrEmpty(value) == false)
                    iHttpConnectionType = value;
                else
                    iHttpConnectionType = "KeepAlive";
            }
        }

        public string HttpMethod
        {
            get { return iHttpMethod; }
            set
            {
                if (string.IsNullOrEmpty(value) == false)
                    iHttpMethod = value;
                else
                    iHttpMethod = "GET";
            }
        }

        public enumRTPLostSearch RTPLostSearch
        {
            get { return iRTPLostSearch; }
            set { iRTPLostSearch = value; }
        }

        public bool HostURI
        {
            get { return iHostURI; }
            set { iHostURI = value; }
        }

        public enumStatus Status
        {
            get { return iStatus; }
        }

        public enumRTCPControlType RTCPControlType
        {
            get { return iRTCPControlType; }
            set { iRTCPControlType = value; }
        }

        public bool SendAuthoraiztionWhenFirstConnected
        {
            get { return iSendAuthFirst; }
            set { iSendAuthFirst = value; }
        }

        public RTSPAuthorization Authoraiztion
        {
            get { return iRTSPAuth; }
            set { iRTSPAuth = value; }
        }

        public enumProtocolType ProtocolType
        {
            get { return iProtocolType; }
            set { iProtocolType = value; }
        }

        public string MemoryReport()
        {
            // iFrameRawData
            // iBuffer
            // iRTPFrameQueue
            // iStreamList
            string S = string.Empty;

            if (iFrameRawData != null)
                S += "FrameRawData count:" + iFrameRawData.Count + "\r\n";

            if (iBuffer != null)
                S += "Buffer count:" + iBuffer.Length + "\r\n";

            if (iStreamList != null)
                S += "StreamList count:" + iStreamList.Count + "\r\n";

            return S;
        }

        public bool CheckIsStreamPort(int Port)
        {
            bool RetValue = false;

            if (iRegStreamPort.ContainsKey(Port))
                RetValue = true;

            return RetValue;
        }

        public bool CheckIsControlPort(int Port)
        {
            bool RetValue = false;

            if (iRegControlPort.ContainsKey(Port))
                RetValue = true;

            return RetValue;
        }

        public void SendFrame(int PayloadType, uint RTPTimestamp, int Channel, byte[] Data, bool Marker)
        {
            RTP RTPFrame = null;
            byte[] RTPFrameBytes = null;

            if (iSendOutFrameSequenceNumber > 65535)
                iSendOutFrameSequenceNumber = 0;

            RTPFrame = new RTP(Data, null, PayloadType, iSendOutFrameSequenceNumber, RTPTimestamp);
            RTPFrame.SSRC = 11223344;
            RTPFrame.Marker = Marker;

            iSendOutFrameSequenceNumber++;

            RTPFrameBytes = RTPFrame.ToByteArray();

            if (iProtocolType == enumProtocolType.RTPOverUDP)
            {
                foreach (RTSPClientStream EachSC in iStreamList)
                {
                    if ((EachSC.ControlPort == Channel))
                    {
                        try { EachSC.SendControl(RTPFrameBytes); }
                        catch (Exception ex) { }
                    }
                    else if ((EachSC.StreamPort == Channel))
                    {
                        try { EachSC.SendStream(RTPFrameBytes); }
                        catch (Exception ex) { }
                    }
                }
            }
            else
            {
                List<byte> SendList = new List<byte>();
                byte[] SendBuffer = null;

                SendList.AddRange(new byte[] { 0x24, System.Convert.ToByte(Channel) });
                SendList.Add((byte)(RTPFrameBytes.Length / 256));
                SendList.Add((byte)(RTPFrameBytes.Length % 256));
                SendList.AddRange(RTPFrameBytes);

                SendBuffer = SendList.ToArray();

                if (iBaseSender != null)
                {
                    if (iBaseSender.GetSourceTCP != null)
                    {
                        if (iBaseSender.GetSourceTCP.Connected)
                        {
                            try { iBaseSender.GetSourceTCP.Client.Send(SendBuffer); }
                            catch (Exception ex) { }
                        }
                    }
                }
            }
        }

        public bool SendPauseCmd()
        {
            RTSPRequest RC = null;

            if (iHostURI)
                RC = iBaseSender.CreateRequest("PAUSE", iURL);
            else
                RC = iBaseSender.CreateRequest("PAUSE", new System.Uri(iURL).PathAndQuery);

            return RTSPSendCmd(RC);
        }

        public bool SendPlayCmd(float Scale)
        {
            RTSPRequest RC = null;
            bool RetValue;

            if (iHostURI)
                RC = iBaseSender.CreateRequest("PLAY", iURL);
            else
                RC = iBaseSender.CreateRequest("PLAY", new System.Uri(iURL).PathAndQuery);

            RC.Header.Set("Range", "npt=0.000-");
            RC.Header.Set("Scale", Scale.ToString());

            RetValue = RTSPSendCmd(RC);

            return RetValue;
        }

        public bool SendRTSPCmd(string Cmd)
        {
            return SendRTSPCmd(Cmd, null);
        }

        public bool SendRTSPCmd(string Cmd, RTSPHeaderSet Headers)
        {
            return SendRTSPCmd(Cmd, Headers, null);
        }

        public bool SendRTSPCmd(string Cmd, RTSPHeaderSet Headers, byte[] Body)
        {
            RTSPRequest RC = null;

            if (iHostURI)
                RC = iBaseSender.CreateRequest(Cmd, iURL);
            else
                RC = iBaseSender.CreateRequest(Cmd, new System.Uri(iURL).PathAndQuery);

            if (Headers != null)
            {
                if (Headers.Count > 0)
                {
                    foreach (string EachKey in Headers.GetKeys())
                    {
                        RC.Header.Set(EachKey, Headers[EachKey]);
                    }
                }
            }

            if (Body != null)
                RC.Body = Body;


            return RTSPSendCmd(RC);
        }

        private bool RTSPSendCmd(RTSPRequest RC)
        {
            string TmpURI;
            RTSPResponse RP = null;
            bool NeedConnection = true;
            bool SenderConnectionSuccess = false;
            bool RetValue = false;

            if (iIsNeedAuth)
            {
                if (iRTSPAuth != null)
                    RC.Header.Set("Authorization", iRTSPAuth.ToHeaderString(iURL, RC.Method));
                else
                    throw new RTSPException("Authorization require", RC, null);
            }

            if (iSessionID != null)
            {
                if (iSessionID != string.Empty)
                    RC.Header.Set("Session", iSessionID);
            }

            TmpURI = RC.URI;

            RTSPCmdSendBefore?.Invoke(this, RC);

            if (RC.URI.Trim().ToUpper() != TmpURI.Trim().ToUpper())
                iURL = RC.URI;

            if ((iProtocolType == enumProtocolType.RTSPOverHTTP_B64_Type2) || (iProtocolType == enumProtocolType.RTSPOverHTTP_Text_Type2))
            {
                if (iBaseSender != null)
                {
                    string SendString;
                    int ContentLength;
                    System.Uri ServerURI = new System.Uri(iURL);

                    if (iConnectionClose)
                    {
                        if (iBaseSender.GetSourceTCP != null)
                        {
                            if (iBaseSender.GetSourceTCP.Connected)
                            {
                                try { iBaseSender.CloseServer(); }
                                catch (Exception ex) { }
                            }
                        }
                    }

                    if (iBaseSender != null)
                    {
                        if (iBaseSender.GetSourceTCP != null)
                        {
                            if (iBaseSender.GetSourceTCP.Connected)
                                NeedConnection = false;
                        }
                    }

                    if (NeedConnection)
                    {
                        try
                        {
                            iBaseSender.ConnectServer(iConnectedRemoteIP, iConnectedRemotePort);
                            SenderConnectionSuccess = true;
                        }
                        catch (Exception ex)
                        {
                            iBaseSender.CloseServer();
                        }
                    }
                    else
                        SenderConnectionSuccess = true;


                    ContentLength = RC.ToByteArray(iBaseSender.CvtBase64Send).Length;

                    if ((iIsNeedAuth) || (iSendAuthFirst))
                        SendString = "POST " + ServerURI.PathAndQuery + " HTTP/1.1\r\n" +
                                     "x-sessioncookie: " + xSession + "\r\n" +
                                     "Content-Type: application/x-rtsp-tunnelled" + "\r\n" +
                                     "Content-Length: " + ContentLength + "\r\n" +
                                     "User-Agent: " + iUserAgent + "\r\n" +
                                     "Host: " + ServerURI.Host + ":" + ServerURI.Port + "\r\n" +
                                     "Cache-Control: no-cache" + "\r\n" +
                                     "Authorization: " + iRTSPAuth.ToHeaderString(ServerURI.PathAndQuery, "POST") +
                                     "\r\n\r\n";
                    else
                        SendString = "POST " + ServerURI.PathAndQuery + " HTTP/1.1\r\n" +
                                     "x-sessioncookie: " + xSession + "\r\n" +
                                     "Content-Type: application/x-rtsp-tunnelled\r\n" +
                                     "Content-Length: " + ContentLength + "\r\n" +
                                     "User-Agent: " + iUserAgent + "\r\n" +
                                     "Host: " + ServerURI.Host + ":" + ServerURI.Port + "\r\n" +
                                     "Cache-Control: no-cache" +
                                     "\r\n\r\n";

                    iBaseSender.GetSourceTCP.Client.Send(System.Text.Encoding.UTF8.GetBytes(SendString));

                    if (iBaseSender.WriteRequest(RC))
                    {
                        if (NeedConnection)
                        {
                            RTSPResponse HttpResp = null;

                            // 等待 HTTP 回應
                            HttpResp = iBaseSender.WaitResponse(10000);

                            if (iConnectionClose)
                            {
                                if (iBaseSender != null)
                                    iBaseSender.CloseServer();
                            }
                        }

                        if (iIsBeginReceived)
                            iLastRC = RC;

                        RetValue = true;
                    }
                }
            }
            else if (iBaseSender.WriteRequest(RC))
            {
                if (iIsBeginReceived)
                    iLastRC = RC;

                RetValue = true;
            }

            return RetValue;
        }

        public void UDPListenRTP(string RemoteIP, int LocalPortStream, int LocalPortControl, int RemotePortStream, int RemotePortControl)
        {
            RTSPClientStream SC = null;

            iConnectedRemoteIP = RemoteIP;

            SC = new RTSPClientStream(string.Empty, RTSPClientStream.enumPortType.RTPOverUDP, LocalPortStream, LocalPortControl);
            SC.Connect(System.Net.IPAddress.Parse(RemoteIP), RemotePortStream, RemotePortControl);

            iStreamList.Add(SC);

            BeginRTPReceive();
        }

        public void RequestURL(string URL, bool CallPlay)
        {
            RTSPRequest RC = null;
            RTSPResponse RP = null;
            System.Uri ServerURI = null;
            List<RTSPClientStream> iSetupStream = new List<RTSPClientStream>();
            string TmpURI;
            bool AllowRTSPStart = false;
            bool IsRTSPOverHttp = false;
            bool RTSPOverHttpSuccess = false;
            bool FirstSetup = true;
            RTSPClientStream SetupSC = null;
            string HostStr;
            string RtspRequestURI;
            Random r = new Random();

            if (iStatus != enumStatus.None)
            {
                DateTime CallStartDate = DateTime.Now;

                CloseServer();

                while (true)
                {
                    if (iStatus == enumStatus.None)
                        break;

                    if (DateTime.Now >= CallStartDate)
                    {
                        if (DateTime.Now.Subtract(CallStartDate).TotalSeconds >= 2)
                            break;
                    }
                    else
                        CallStartDate = DateTime.Now;

                    System.Threading.Thread.Sleep(100);
                }
            }

            iStatus = enumStatus.None;
            iURL = URL;

            while (true)
            {
                bool NextConnectionLoop = false;

                iBasePortIndex = 0;
                iHttpHeaderSended = false;
                iConnectionClose = false;
                iRTCPServerReport = false;
                iIsNeedAuth = false;
                iHttpBoundary = null;
                iBoundaryHash.Clear();
                iWaitingBoundary = false;
                iLastReceivedRTP = null;
                iLastRC = null;

                xSession = r.Next(1024, 65535);

                InitProtocol();

                iLastBoundarySearchIndex = 0;
                iFrameRawData.Clear();
                iRegStreamPort.Clear();
                iRegControlPort.Clear();

                RtspRequestURI = GetRequestURI("rtsp");

                try { ServerURI = new System.Uri(iURL); }
                catch (Exception ex) { }

                if (ServerURI != null)
                {
                    bool ConnectSuccess = false;

                    iBaseRecv = new RTSPClientBase();

                    switch (iProtocolType)
                    {
                        case enumProtocolType.RTSPOverHTTP_B64:
                        case enumProtocolType.RTSPOverHTTP_Text:
                        case enumProtocolType.RTSPOverHTTP_B64_Type2:
                        case enumProtocolType.RTSPOverHTTP_Text_Type2:
                            IsRTSPOverHttp = true;

                            if (ServerURI.IsDefaultPort)
                                iConnectedRemotePort = 80;
                            else
                                iConnectedRemotePort = ServerURI.Port;

                            break;
                        default:
                            if (ServerURI.IsDefaultPort)
                            {
                                if (ServerURI.Scheme.Trim().ToUpper() == "http".ToUpper())
                                    iConnectedRemotePort = 80;
                                else
                                    iConnectedRemotePort = 554;
                            }
                            else
                                iConnectedRemotePort = ServerURI.Port;

                            break;
                    }

                    iConnectedRemoteIP = ServerURI.Host;

                    try
                    {
                        iBaseRecv.ConnectServer(iConnectedRemoteIP, iConnectedRemotePort);
                        ConnectSuccess = true;
                    }
                    catch (Exception ex)
                    {
                        try { iBaseRecv.CloseServer(); }
                        catch (Exception ex2) { }

                        iBaseRecv = null;

                        throw ex;
                    }

                    iIsNeedAuth = false;

                    if (ServerURI.Port == 80)
                        HostStr = ServerURI.Host;
                    else
                        HostStr = ServerURI.Host + ":" + ServerURI.Port;

                    if (IsRTSPOverHttp)
                    {
                        string SendMethod;
                        string SendHeader;
                        bool HttpNeedAuth = false;
                        RTSPAuthorization HttpAuth = null;

                        for (int I = 1; I <= 2; I++)
                        {
                            bool NeedReconnect = true;

                            SendHeader = "x-sessioncookie: " + xSession + "\r\n" +
                                         "User-Agent: " + iUserAgent + "\r\n" +
                                         "Host: " + HostStr + "\r\n" +
                                         "Cache-Control: no-cache" + "\r\n";

                            SendMethod = "GET " + ServerURI.PathAndQuery + " HTTP/1.1";

                            if (iBaseRecv.GetSourceTCP != null)
                            {
                                if (iBaseRecv.GetSourceTCP.Connected == true)
                                    NeedReconnect = false;
                            }

                            if (NeedReconnect)
                            {
                                iConnectedRemoteIP = ServerURI.Host;
                                iBaseRecv.ConnectServer(iConnectedRemoteIP, iConnectedRemotePort);
                            }

                            if ((HttpNeedAuth) || (iSendAuthFirst))
                            {
                                if (HttpAuth == null)
                                    HttpAuth = new RTSPAuthorization(this.Authoraiztion, "BASIC");

                                iBaseRecv.GetSourceTCP.Client.Send(System.Text.Encoding.UTF8.GetBytes(SendMethod + "\r\n" + SendHeader + "Authorization: " + HttpAuth.ToHeaderString(ServerURI.PathAndQuery, "GET") + "\r\n\r\n"));
                            }
                            else
                                iBaseRecv.GetSourceTCP.Client.Send(System.Text.Encoding.UTF8.GetBytes(SendMethod + "\r\n" + SendHeader + "\r\n"));

                            RP = iBaseRecv.WaitResponse(10000);
                            if (RP != null)
                            {
                                if (RP.StatusCode == "200")
                                {
                                    // Content-Type: application/x-rtsp-tunnelled
                                    if ((RP.Header["Content-Type"]).ToUpper() == "application/x-rtsp-tunnelled".ToUpper())
                                    {
                                        string SendString = string.Empty;
                                        int SendLength;
                                        System.Net.IPEndPoint LocalEP = null;
                                        bool NeedConnection = true;
                                        bool SenderConnectionSuccess = false;

                                        // 檢查 ConnectionClose
                                        if (RP.Header["Connection"] != null)
                                        {
                                            if ((RP.Header["Connection"]).Trim().ToUpper() == "Close".Trim().ToUpper())
                                                iConnectionClose = true;
                                        }
                                        else
                                        {
                                            switch (iProtocolType)
                                            {
                                                case enumProtocolType.RTSPOverHTTP_B64_Type2:
                                                case enumProtocolType.RTSPOverHTTP_Text_Type2:
                                                    {
                                                        // Connection 沒有定義, 設定為 Close
                                                        iConnectionClose = true;
                                                        break;
                                                    }
                                            }
                                        }

                                        AllowRTSPStart = true;
                                        break;
                                    }
                                }
                                else if (RP.StatusCode == "401")
                                {
                                    // 需要驗證
                                    if (Authoraiztion != null)
                                    {
                                        if (RP.Header.HasKey("WWW-Authenticate"))
                                        {
                                            bool AllowReconnect = true;
                                            RTSPAuthorization BasicAuth = null;
                                            RTSPAuthorization DigestAuth = null;
                                            string[] WWWAuthList = null;

                                            WWWAuthList = RP.Header.GetValues("WWW-Authenticate");
                                            if (WWWAuthList != null)
                                            {
                                                foreach (string AuthValue in WWWAuthList)
                                                {
                                                    RTSPAuthorization _TmpAuth = null;

                                                    if (string.IsNullOrEmpty(AuthValue) == false)
                                                    {
                                                        try { _TmpAuth = new RTSPAuthorization(this.Authoraiztion, AuthValue); }
                                                        catch (Exception ex) { }

                                                        if (_TmpAuth != null)
                                                        {
                                                            switch (_TmpAuth.AuthType)
                                                            {
                                                                case RTSPAuthorization.enumWWWAuthorizeType.BASIC:
                                                                    BasicAuth = _TmpAuth;
                                                                    break;
                                                                case RTSPAuthorization.enumWWWAuthorizeType.DIGEST:
                                                                    DigestAuth = _TmpAuth;
                                                                    break;
                                                            }
                                                        }
                                                    }
                                                }
                                            }

                                            if ((BasicAuth != null) || (DigestAuth != null))
                                            {
                                                HttpNeedAuth = true;

                                                if (HttpAuth == null)
                                                {
                                                    // 優先使用 Basic 驗證
                                                    if (BasicAuth != null)
                                                        HttpAuth = BasicAuth;
                                                    else if (DigestAuth != null)
                                                        HttpAuth = DigestAuth;
                                                }
                                            }

                                            if (HttpAuth != null)
                                            {
                                                if (HttpAuth.AuthType == RTSPAuthorization.enumWWWAuthorizeType.DIGEST)
                                                    AllowReconnect = false;
                                            }

                                            if (AllowReconnect)
                                            {
                                                if (iBaseRecv != null)
                                                    iBaseRecv.CloseServer();
                                            }
                                        }
                                    }
                                }
                                else if (RP.StatusCode == "302")
                                {
                                    // Redirect
                                    if (string.IsNullOrEmpty(RP.Header["Location"]) == false)
                                    {
                                        if (iURL != RP.Header["Location"])
                                        {
                                            iBaseRecv.CloseServer();

                                            NextConnectionLoop = true;
                                            iURL = RP.Header["Location"];

                                            break;
                                        }
                                    }
                                }
                            }
                            else
                                // 無回應
                                break;
                        }

                        if (RP != null)
                        {
                            if ((RP.StatusCode != "200") & (NextConnectionLoop == false))
                                throw new RTSPException(RP.StatusMsg, null, RP);
                        }
                        else
                            throw new RTSPException("No Connection", null, null);
                    }
                    else if ((iProtocolType == enumProtocolType.HttpPush) || (iProtocolType == enumProtocolType.HttpRaw))
                    {
                        string SendMethod;
                        string SendHeader;
                        bool HttpNeedAuth = false;
                        RTSPAuthorization HttpAuth = null;

                        iHttpHeaderSended = true;

                        for (int I = 1; I <= 2; I++)
                        {
                            bool NeedReconnect = true;

                            SendHeader = "User-Agent: " + iUserAgent + "\r\n" +
                                         "Host: " + HostStr + "\r\n" +
                                         "Connection: " + iHttpConnectionType + "\r\n" +
                                         "Cache-Control: no-cache\r\n";

                            if (string.IsNullOrEmpty(iHttpContentType) == false)
                                SendHeader += "Content-Type: " + iHttpContentType + "\r\n";

                            SendMethod = iHttpMethod + " " + ServerURI.PathAndQuery + " HTTP/1.1";
                            if (iBaseRecv.GetSourceTCP != null)
                            {
                                if (iBaseRecv.GetSourceTCP.Connected == true)
                                    NeedReconnect = false;
                            }

                            if (NeedReconnect)
                            {
                                iConnectedRemoteIP = ServerURI.Host;
                                iBaseRecv.ConnectServer(iConnectedRemoteIP, iConnectedRemotePort);
                            }

                            if ((HttpNeedAuth) || (iSendAuthFirst))
                            {
                                if (HttpAuth == null)
                                    HttpAuth = new RTSPAuthorization(this.Authoraiztion, "BASIC");

                                iBaseRecv.GetSourceTCP.Client.Send(System.Text.Encoding.UTF8.GetBytes(SendMethod + "\r\n" + SendHeader + "Authorization: " + HttpAuth.ToHeaderString(ServerURI.PathAndQuery, "GET") + "\r\n\r\n"));
                            }
                            else
                                iBaseRecv.GetSourceTCP.Client.Send(System.Text.Encoding.UTF8.GetBytes(SendMethod + "\r\n" + SendHeader + "\r\n"));

                            RP = iBaseRecv.WaitResponse(10000);
                            if (RP != null)
                            {
                                if (RP.StatusCode == "200")
                                {
                                    // Content-Type: multipart/x-mixed-replace; boundary=--EventConnector
                                    if (iProtocolType == enumProtocolType.HttpPush)
                                    {
                                        string[] ContentTypeArray = null;

                                        ContentTypeArray = RP.Header["Content-Type"].Split(";");
                                        if (ContentTypeArray.Length > 1)
                                        {
                                            // 尋找 Boundary
                                            bool FoundBoundaryField = false;

                                            for (int J = 1; J <= ContentTypeArray.Length - 1; J++)
                                            {
                                                string TmpStr = ContentTypeArray[J];

                                                if (TmpStr != string.Empty)
                                                {
                                                    int TmpIndex;

                                                    TmpIndex = TmpStr.IndexOf("=");
                                                    if (TmpIndex != -1)
                                                    {
                                                        string Cmd;
                                                        string Value;

                                                        Cmd = TmpStr.Substring(0, TmpIndex).Trim();
                                                        Value = TmpStr.Substring(TmpIndex + 1).Trim();

                                                        if (string.IsNullOrEmpty(Value) == false)
                                                        {
                                                            if (Value.StartsWith("\"") & Value.EndsWith("\""))
                                                                Value = Value.Substring(1, Value.Length - 2);
                                                        }

                                                        if (Cmd.ToUpper() == "Boundary".ToUpper())
                                                        {
                                                            FoundBoundaryField = true;

                                                            iHttpBoundary = System.Text.Encoding.UTF8.GetBytes(Value);
                                                            foreach (byte EachByte in iHttpBoundary)
                                                            {
                                                                if (iBoundaryHash.Contains(EachByte) == false)
                                                                    iBoundaryHash.Add(EachByte);
                                                            }

                                                            break;
                                                        }
                                                    }
                                                }
                                            }

                                            if ((iHttpBoundary != null) && (FoundBoundaryField == true))
                                            {
                                                byte[] MoreData = null;

                                                if (RP.Body != null)
                                                {
                                                    if (RP.Body.Length > 0)
                                                        iFrameRawData.AddRange(RP.Body);
                                                }

                                                MoreData = iBaseRecv.GetAndFlushBuffer();

                                                if (MoreData != null)
                                                {
                                                    if (MoreData.Length > 0)
                                                        iFrameRawData.AddRange(MoreData);
                                                }

                                                AllowRTSPStart = true;

                                                break;
                                            }
                                        }
                                    }
                                    else if (iProtocolType == enumProtocolType.HttpRaw)
                                    {
                                        byte[] MoreData = null;

                                        iBoundaryHeader = RP.Header;

                                        if (RP.Body != null)
                                        {
                                            if (RP.Body.Length > 0)
                                                iFrameRawData.AddRange(RP.Body);
                                        }

                                        MoreData = iBaseRecv.GetAndFlushBuffer();

                                        if (MoreData != null)
                                        {
                                            if (MoreData.Length > 0)
                                                iFrameRawData.AddRange(MoreData);
                                        }

                                        AllowRTSPStart = true;

                                        break;
                                    }
                                }
                                else if (RP.StatusCode == "401")
                                {
                                    // 需要驗證
                                    if (Authoraiztion != null)
                                    {
                                        if (RP.Header.HasKey("WWW-Authenticate"))
                                        {
                                            bool AllowReconnect = true;
                                            RTSPAuthorization BasicAuth = null;
                                            RTSPAuthorization DigestAuth = null;
                                            string[] WWWAuthList = null;

                                            WWWAuthList = RP.Header.GetValues("WWW-Authenticate");
                                            if (WWWAuthList != null)
                                            {
                                                foreach (string AuthValue in WWWAuthList)
                                                {
                                                    RTSPAuthorization _TmpAuth = null;

                                                    if (string.IsNullOrEmpty(AuthValue) == false)
                                                    {
                                                        try { _TmpAuth = new RTSPAuthorization(this.Authoraiztion, AuthValue); }
                                                        catch (Exception ex) { }

                                                        if (_TmpAuth != null)
                                                        {
                                                            switch (_TmpAuth.AuthType)
                                                            {
                                                                case RTSPAuthorization.enumWWWAuthorizeType.BASIC:
                                                                    BasicAuth = _TmpAuth;
                                                                    break;
                                                                case RTSPAuthorization.enumWWWAuthorizeType.DIGEST:
                                                                    DigestAuth = _TmpAuth;
                                                                    break;
                                                            }
                                                        }
                                                    }
                                                }
                                            }

                                            if ((BasicAuth != null) || (DigestAuth != null))
                                            {
                                                HttpNeedAuth = true;

                                                if (HttpAuth == null)
                                                {
                                                    // 優先使用 Basic 驗證
                                                    if (BasicAuth != null)
                                                        HttpAuth = BasicAuth;
                                                    else if (DigestAuth != null)
                                                        HttpAuth = DigestAuth;
                                                }
                                            }

                                            if (HttpAuth != null)
                                            {
                                                if (HttpAuth.AuthType == RTSPAuthorization.enumWWWAuthorizeType.DIGEST)
                                                    AllowReconnect = false;
                                            }

                                            if (AllowReconnect)
                                            {
                                                if (iBaseRecv != null)
                                                    iBaseRecv.CloseServer();
                                            }
                                        }
                                    }
                                }
                                else if (RP.StatusCode == "302")
                                {
                                    // Redirect
                                    if (string.IsNullOrEmpty(RP.Header["Location"]) == false)
                                    {
                                        if (iURL != RP.Header["Location"])
                                        {
                                            iBaseRecv.CloseServer();

                                            NextConnectionLoop = true;
                                            iURL = RP.Header["Location"];

                                            break;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                // 修正 DIGEST 驗證斷線(通常不應該斷線)
                                if ((I == 2) && (HttpAuth.AuthType == RTSPAuthorization.enumWWWAuthorizeType.DIGEST))
                                {
                                    // 發生 DIGEST 斷線
                                    I = 1;
                                    if (iBaseRecv != null)
                                        iBaseRecv.CloseServer();
                                }
                            }
                        }

                        if (RP != null)
                        {
                            if ((RP.StatusCode != "200") && (NextConnectionLoop == false))
                                throw new RTSPException(RP.StatusMsg, null, RP);
                        }
                    }
                    else
                    {
                        iBaseRecv.CvtBase64Send = false;
                        iBaseSender = iBaseRecv;
                        AllowRTSPStart = true;
                        iHttpHeaderSended = true;
                    }

                    if (AllowRTSPStart)
                    {
                        if ((iProtocolType != enumProtocolType.HttpPush) && (iProtocolType != enumProtocolType.HttpRaw))
                        {
                            int RecheckCount = 0;

                            iSessionID = string.Empty;
                            iStatus = enumStatus.OPTIONS;

                            while (true)
                            {
                                bool NeedConnection = true;
                                bool SenderConnectionSuccess = false;
                                bool ExitDo = false;

                                // 檢查 Sender 是否仍在連線
                                // Type2 檢查是否需要斷線
                                if (iBaseSender == null)
                                    iBaseSender = new RTSPClientBase();

                                if ((iProtocolType == enumProtocolType.RTSPOverHTTP_B64_Type2) || (iProtocolType == enumProtocolType.RTSPOverHTTP_Text_Type2))
                                {
                                    if (iConnectionClose)
                                    {
                                        if (iBaseSender.GetSourceTCP != null)
                                        {
                                            if (iBaseSender.GetSourceTCP.Connected)
                                            {
                                                try { iBaseSender.CloseServer(); }
                                                catch (Exception ex) { }
                                            }
                                        }
                                    }
                                }

                                if (iBaseSender.GetSourceTCP != null)
                                {
                                    if (iBaseSender.GetSourceTCP.Connected)
                                        NeedConnection = false;
                                }

                                if (NeedConnection)
                                {
                                    try
                                    {
                                        iBaseSender.ConnectServer(iConnectedRemoteIP, iConnectedRemotePort);
                                        SenderConnectionSuccess = true;
                                    }
                                    catch (Exception ex)
                                    {
                                        iBaseSender.CloseServer();
                                    }
                                }
                                else
                                    SenderConnectionSuccess = true;

                                if (SenderConnectionSuccess)
                                {
                                    SetupSC = null;

                                    if ((iProtocolType == enumProtocolType.RTSPOverHTTP_B64) || (iProtocolType == enumProtocolType.RTSPOverHTTP_B64_Type2))
                                        iBaseSender.CvtBase64Send = true;

                                    switch (iStatus)
                                    {
                                        case enumStatus.None:
                                            // Closed
                                            ExitDo = true;

                                            break;
                                        case enumStatus.OPTIONS:
                                            RC = iBaseSender.CreateRequest("OPTIONS", RtspRequestURI);

                                            break;
                                        case enumStatus.DESCRIBE:
                                            RC = iBaseSender.CreateRequest("DESCRIBE", RtspRequestURI);

                                            RC.Header.Set("Accept", "application/sdp");

                                            break;
                                        case enumStatus.SETUP:
                                            if (FirstSetup)
                                            {
                                                FirstSetup = false;

                                                if (iStreamList.Count <= 0)
                                                    AddRTSPClientStream(RtspRequestURI);

                                                lock (iStreamList)
                                                {
                                                    foreach (RTSPClientStream EachSC in iStreamList)
                                                    {
                                                        iSetupStream.Add(EachSC);
                                                    }
                                                }
                                            }

                                            if (iSetupStream.Count > 0)
                                            {
                                                SetupSC = iSetupStream[0];
                                                iSetupStream.RemoveAt(0);

                                                RC = iBaseSender.CreateRequest("SETUP", SetupSC.SETUP_URL);

                                                switch (iProtocolType)
                                                {
                                                    case enumProtocolType.RTPOverTCP:
                                                        RC.Header.Set("Transport", "RTP/AVP/TCP;unicast;interleaved=" + SetupSC.StreamPort + "-" + SetupSC.ControlPort);

                                                        break;
                                                    case enumProtocolType.RTPOverUDP:
                                                        RC.Header.Set("Transport", "RTP/AVP;unicast;client_port=" + SetupSC.StreamPort + "-" + SetupSC.ControlPort);

                                                        break;
                                                    case enumProtocolType.RTSPOverHTTP_B64:
                                                    case enumProtocolType.RTSPOverHTTP_Text:
                                                    case enumProtocolType.RTSPOverHTTP_B64_Type2:
                                                    case enumProtocolType.RTSPOverHTTP_Text_Type2:
                                                        RC.Header.Set("Transport", "RTP/AVP/TCP;unicast");

                                                        break;
                                                }
                                            }

                                            break;
                                        case enumStatus.PLAY:
                                            if (CallPlay)
                                            {
                                                RC = iBaseSender.CreateRequest("PLAY", RtspRequestURI);
                                                RC.Header.Set("Range", "npt=0.000-");
                                            }
                                            else
                                                ExitDo = true;

                                            break;
                                        case enumStatus.Ready:
                                            ExitDo = true;

                                            break;
                                    }

                                    if (ExitDo)
                                        break;

                                    if ((iIsNeedAuth) || (iSendAuthFirst))
                                    {
                                        if (iRTSPAuth != null)
                                            RC.Header.Set("Authorization", iRTSPAuth.ToHeaderString(RtspRequestURI, RC.Method));
                                        else
                                            throw new RTSPException("Authorization require", RC, null);
                                    }

                                    if (iSessionID != null)
                                    {
                                        if (iSessionID != string.Empty)
                                            RC.Header.Set("Session", iSessionID);
                                    }

                                    TmpURI = RC.URI;

                                    // 傳送前清除接收緩衝區資料
                                    iBaseRecv.GetAndFlushBuffer();

                                    RTSPCmdSendBefore?.Invoke(this, RC);

                                    if (RC.URI.Trim().ToUpper() != TmpURI.Trim().ToUpper())
                                        iURL = RC.URI;

                                    if ((iProtocolType == enumProtocolType.RTSPOverHTTP_B64) ||
                                        (iProtocolType == enumProtocolType.RTSPOverHTTP_B64_Type2) ||
                                        (iProtocolType == enumProtocolType.RTSPOverHTTP_Text) ||
                                        (iProtocolType == enumProtocolType.RTSPOverHTTP_Text_Type2))
                                    {
                                        if (iHttpHeaderSended == false)
                                        {
                                            // 必須包含 Http 表頭
                                            string SendString;
                                            int ContentLength;

                                            if ((iProtocolType == enumProtocolType.RTSPOverHTTP_B64_Type2) || (iProtocolType == enumProtocolType.RTSPOverHTTP_Text_Type2))
                                                ContentLength = RC.ToByteArray(iBaseSender.CvtBase64Send).Length;
                                            else
                                                // Type 1
                                                // Content 永遠是 32767
                                                ContentLength = 32767;

                                            if ((iIsNeedAuth) || (iSendAuthFirst))
                                                SendString = "POST " + ServerURI.PathAndQuery + " HTTP/1.1\r\n" +
                                                             "x-sessioncookie: " + xSession + "\r\n" +
                                                             "Content-Type: application/x-rtsp-tunnelled" + "\r\n" +
                                                             "Content-Length: " + ContentLength + "\r\n" +
                                                             "User-Agent: " + iUserAgent + "\r\n" +
                                                             "Host: " + HostStr + "\r\n" +
                                                             "Cache-Control: no-cache" + "\r\n" +
                                                             "Authorization: " + iRTSPAuth.ToHeaderString(ServerURI.PathAndQuery, "POST") + "\r\n\r\n";
                                            else
                                                SendString = "POST " + ServerURI.PathAndQuery + " HTTP/1.1\r\n" +
                                                             "x-sessioncookie: " + xSession + "\r\n" +
                                                             "Content-Type: application/x-rtsp-tunnelled" + "\r\n" +
                                                             "Content-Length: " + ContentLength + "\r\n" +
                                                             "User-Agent: " + iUserAgent + "\r\n" +
                                                             "Host: " + HostStr + "\r\n" +
                                                             "Cache-Control: no-cache" + "\r\n\r\n";

                                            iBaseSender.GetSourceTCP.Client.Send(System.Text.Encoding.UTF8.GetBytes(SendString));

                                            System.Threading.Thread.Sleep(500);

                                            if (iBaseSender.GetSourceTCP.Available > 0)
                                            {
                                                RTSPResponse PostRP = null;
                                                bool AllowExit = true;

                                                PostRP = iBaseSender.WaitResponse(10000);
                                                if (PostRP != null)
                                                {
                                                    if (PostRP.StatusCode == "401")
                                                    {
                                                        // Http Post 需要驗證
                                                        if (RecheckCount <= 0)
                                                        {
                                                            bool AllowReconnect = true;

                                                            RecheckCount++;
                                                            iIsNeedAuth = true;

                                                            if (PostRP.Header.HasKey("WWW-Authenticate"))
                                                            {
                                                                if (iRTSPAuth != null)
                                                                {
                                                                    string[] WWWAuthList = null;
                                                                    RTSPAuthorization BasicAuth = null;
                                                                    RTSPAuthorization DigestAuth = null;

                                                                    WWWAuthList = PostRP.Header.GetValues("WWW-Authenticate");
                                                                    if (WWWAuthList != null)
                                                                    {
                                                                        foreach (string AuthStr in WWWAuthList)
                                                                        {
                                                                            RTSPAuthorization _TmpAuth = null;

                                                                            if (string.IsNullOrEmpty(AuthStr) == false)
                                                                            {
                                                                                _TmpAuth = new RTSPAuthorization(iRTSPAuth, AuthStr);

                                                                                switch (_TmpAuth.AuthType)
                                                                                {
                                                                                    case RTSPAuthorization.enumWWWAuthorizeType.BASIC:
                                                                                        BasicAuth = _TmpAuth;

                                                                                        break;
                                                                                    case RTSPAuthorization.enumWWWAuthorizeType.DIGEST:
                                                                                        DigestAuth = _TmpAuth;

                                                                                        break;
                                                                                }
                                                                            }
                                                                        }
                                                                    }

                                                                    if (BasicAuth != null)
                                                                        iRTSPAuth = BasicAuth;
                                                                    else if (DigestAuth != null)
                                                                        iRTSPAuth = DigestAuth;
                                                                }
                                                            }

                                                            if (iRTSPAuth != null)
                                                            {
                                                                if (iRTSPAuth.AuthType == RTSPAuthorization.enumWWWAuthorizeType.DIGEST)
                                                                    AllowReconnect = false;
                                                            }

                                                            // 關閉 Sender 避免繼續傳送會造成無回應
                                                            if (AllowReconnect)
                                                            {
                                                                if (iBaseSender != null)
                                                                    iBaseSender.CloseServer();
                                                            }
                                                        }
                                                        else
                                                            throw new RTSPException("Authorization failure", RC, PostRP);
                                                    }
                                                    else
                                                        RecheckCount = 0;
                                                }
                                                else
                                                    RecheckCount = 0;
                                            }
                                            else
                                                RecheckCount = 0;
                                        }
                                    }

                                    if (RecheckCount <= 0)
                                    {
                                        if (iBaseSender.WriteRequest(RC))
                                        {
                                            // 等待 500ms
                                            if ((iProtocolType == enumProtocolType.RTSPOverHTTP_B64_Type2) || (iProtocolType == enumProtocolType.RTSPOverHTTP_Text_Type2))
                                            {
                                                if (iHttpHeaderSended == false)
                                                {
                                                    RTSPResponse HttpResp = null;
                                                    iHttpHeaderSended = true;

                                                    // 等待 HTTP 回應
                                                    HttpResp = iBaseSender.WaitResponse(10000);

                                                    if ((iProtocolType == enumProtocolType.RTSPOverHTTP_B64_Type2) || (iProtocolType == enumProtocolType.RTSPOverHTTP_Text_Type2))
                                                    {
                                                        iHttpHeaderSended = false;

                                                        if (iConnectionClose)
                                                        {
                                                            if (iBaseSender != null)
                                                                iBaseSender.CloseServer();  // 斷線不釋放資源
                                                        }
                                                    }
                                                }
                                            }
                                            else
                                                iHttpHeaderSended = true;

                                            RP = iBaseRecv.WaitResponse(10000);

                                            if (RP != null)
                                            {
                                                switch (RP.StatusCode)
                                                {
                                                    case "200":
                                                        // ok
                                                        if (RP.Header["Session"] != null)
                                                        {
                                                            int TmpIndex;

                                                            iSessionID = RP.Header["Session"];

                                                            TmpIndex = iSessionID.IndexOf(";");
                                                            if (TmpIndex != -1)
                                                                iSessionID = iSessionID.Substring(0, TmpIndex).Trim();
                                                        }

                                                        // 指令完成, Status 進入下一階段
                                                        switch (iStatus)
                                                        {
                                                            case enumStatus.OPTIONS:
                                                                iStatus = enumStatus.DESCRIBE;

                                                                break;
                                                            case enumStatus.DESCRIBE:
                                                                iStatus = enumStatus.SETUP;

                                                                break;
                                                            case enumStatus.SETUP:
                                                                string TransportString;

                                                                TransportString = RP.Header["Transport"];

                                                                switch (iProtocolType)
                                                                {
                                                                    case enumProtocolType.RTPOverTCP:
                                                                    case enumProtocolType.RTSPOverHTTP_B64:
                                                                    case enumProtocolType.RTSPOverHTTP_Text:
                                                                    case enumProtocolType.RTSPOverHTTP_B64_Type2:
                                                                    case enumProtocolType.RTSPOverHTTP_Text_Type2:
                                                                        // RTP/AVP/TCP;unicast;interleaved=10-11;mode="PLAY"
                                                                        string[] StrArray = null;

                                                                        if (RP.Header["Transport"] != null)
                                                                        {
                                                                            StrArray = RP.Header["Transport"].Split(";");

                                                                            foreach (string EachStr in StrArray)
                                                                            {
                                                                                int TmpIndex;
                                                                                string TmpCmd;
                                                                                string TmpValue;

                                                                                TmpIndex = EachStr.IndexOf("=");
                                                                                if (TmpIndex != -1)
                                                                                {
                                                                                    TmpCmd = EachStr.Substring(0, TmpIndex);
                                                                                    TmpValue = EachStr.Substring(TmpIndex + 1);

                                                                                    if (TmpCmd.Trim().ToUpper() == "Interleaved".ToUpper())
                                                                                    {
                                                                                        int DataChannel;
                                                                                        int RTCPChannel;

                                                                                        TmpIndex = TmpValue.IndexOf("-");
                                                                                        if (TmpIndex != -1)
                                                                                        {
                                                                                            DataChannel = Convert.ToInt32(TmpValue.Substring(0, TmpIndex));
                                                                                            RTCPChannel = Convert.ToInt32(TmpValue.Substring(TmpIndex + 1));


                                                                                            // --- FIX ME
                                                                                            if (iRegStreamPort.ContainsKey(DataChannel) == false)
                                                                                            {
                                                                                                SetupSC.StreamPort = DataChannel;
                                                                                                iRegStreamPort.Add(DataChannel, SetupSC);
                                                                                            }

                                                                                            if (iRegControlPort.ContainsKey(RTCPChannel) == false)
                                                                                            {
                                                                                                SetupSC.ControlPort = RTCPChannel;
                                                                                                iRegControlPort.Add(RTCPChannel, SetupSC);
                                                                                            }
                                                                                        }
                                                                                    }
                                                                                }
                                                                            }
                                                                        }

                                                                        break;
                                                                    case enumProtocolType.RTPOverUDP:
                                                                        string ServerPort = null;
                                                                        string ClientPort = null;
                                                                        string[] ServerPortArray = null;
                                                                        string[] ClientPortArray = null;

                                                                        foreach (string EachStr in TransportString.Split(";"))
                                                                        {
                                                                            if (EachStr.Length >= 12)
                                                                            {
                                                                                if (EachStr.Substring(0, 12) == "server_port=")
                                                                                    ServerPort = EachStr.Substring(12);
                                                                                else if (EachStr.Substring(0, 12) == "client_port=")
                                                                                    ClientPort = EachStr.Substring(12);
                                                                            }
                                                                        }

                                                                        if ((string.IsNullOrEmpty(ServerPort) == false) && (string.IsNullOrEmpty(ClientPort) == false))
                                                                        {
                                                                            ServerPortArray = ServerPort.Split("-");
                                                                            ClientPortArray = ClientPort.Split("-");

                                                                            foreach (RTSPClientStream EachSC in iStreamList)
                                                                            {
                                                                                if ((EachSC.ControlPort == System.Convert.ToInt32(ClientPortArray[1])) &&
                                                                                    (EachSC.StreamPort == System.Convert.ToInt32(ClientPortArray[0])))
                                                                                    EachSC.Connect(System.Net.IPAddress.Parse(iConnectedRemoteIP), System.Convert.ToInt32(ServerPortArray[0]), System.Convert.ToInt32(ServerPortArray[1]));
                                                                            }
                                                                        }

                                                                        break;
                                                                }

                                                                if (iSetupStream.Count <= 0)
                                                                {
                                                                    if (CallPlay)
                                                                        iStatus = enumStatus.PLAY;
                                                                    else
                                                                        iStatus = enumStatus.Ready;
                                                                }

                                                                break;
                                                            case enumStatus.PLAY:
                                                                iStatus = enumStatus.Ready;

                                                                break;
                                                        }

                                                        break;
                                                    case "401":
                                                        // 需要驗證
                                                        if (iIsNeedAuth == false)
                                                        {
                                                            bool AllowReconnect = true;

                                                            iIsNeedAuth = true;

                                                            if (RP.Header.HasKey("WWW-Authenticate"))
                                                            {
                                                                if (iRTSPAuth != null)
                                                                {
                                                                    string[] WWWAuthList = null;
                                                                    RTSPAuthorization BasicAuth = null;
                                                                    RTSPAuthorization DigestAuth = null;

                                                                    WWWAuthList = RP.Header.GetValues("WWW-Authenticate");
                                                                    if (WWWAuthList != null)
                                                                    {
                                                                        foreach (string AuthStr in WWWAuthList)
                                                                        {
                                                                            RTSPAuthorization _TmpAuth = null;

                                                                            if (string.IsNullOrEmpty(AuthStr) == false)
                                                                            {
                                                                                _TmpAuth = new RTSPAuthorization(iRTSPAuth, AuthStr);

                                                                                switch (_TmpAuth.AuthType)
                                                                                {
                                                                                    case RTSPAuthorization.enumWWWAuthorizeType.BASIC:
                                                                                        BasicAuth = _TmpAuth;

                                                                                        break;
                                                                                    case RTSPAuthorization.enumWWWAuthorizeType.DIGEST:
                                                                                        DigestAuth = _TmpAuth;

                                                                                        break;
                                                                                }
                                                                            }
                                                                        }
                                                                    }

                                                                    if (BasicAuth != null)
                                                                        iRTSPAuth = BasicAuth;
                                                                    else if (DigestAuth != null)
                                                                        iRTSPAuth = DigestAuth;
                                                                }
                                                            }

                                                            if (iRTSPAuth != null)
                                                            {
                                                                if (iRTSPAuth.AuthType == RTSPAuthorization.enumWWWAuthorizeType.DIGEST)
                                                                    AllowReconnect = false;
                                                            }

                                                            if (AllowReconnect)
                                                            {
                                                                iHttpHeaderSended = false;

                                                                if (iBaseSender != null)
                                                                {
                                                                    try { iBaseSender.CloseServer(); }
                                                                    catch (Exception ex) { }
                                                                }
                                                            }
                                                        }
                                                        else
                                                            throw new RTSPException("Authorization failure", RC, RP);

                                                        break;
                                                    case "302":
                                                        // Redirect
                                                        if (string.IsNullOrEmpty(RP.Header["Location"]) == false)
                                                        {
                                                            if (iURL != RP.Header["Location"])
                                                            {
                                                                if (iBaseSender != null)
                                                                {
                                                                    try { iBaseSender.CloseServer(); }
                                                                    catch (Exception ex) { }
                                                                }

                                                                try { iBaseRecv.CloseServer(); }
                                                                catch (Exception ex) { }

                                                                NextConnectionLoop = true;
                                                                iURL = RP.Header["Location"];
                                                            }
                                                        }

                                                        break;
                                                    default:
                                                        if (iStatus != enumStatus.PLAY)
                                                            throw new RTSPException(RP.StatusMsg, RC, RP);
                                                        else
                                                            iStatus = enumStatus.Ready;

                                                        break;
                                                }

                                                RTSPCmdSendResponse?.Invoke(this, RC, RP);
                                            }
                                            else
                                            {
                                                RaiseDisconnect();
                                                iStatus = enumStatus.None;
                                            }
                                        }
                                        else
                                        {
                                            RaiseDisconnect();
                                            iStatus = enumStatus.None;
                                        }
                                    }
                                }
                                else
                                {
                                    RaiseDisconnect();
                                    iStatus = enumStatus.None;
                                }
                            }
                        }
                        else
                        {
                            // Http Protocol
                            iWaitingBoundary = true;
                            iStatus = enumStatus.Ready;
                        }

                        if (iStatus != enumStatus.None)
                        {
                            List<byte> NonEmptyBuffer = null;

                            NonEmptyBuffer = iBaseRecv.GetBuffer();

                            if (NonEmptyBuffer.Count > 0)
                                iFrameRawData.AddRange(NonEmptyBuffer.ToArray());

                            if ((iProtocolType == enumProtocolType.HttpPush) || (iProtocolType == enumProtocolType.HttpRaw))
                                // 處理已接收的多餘封包
                                ReceiveProcess();

                            BeginRTPReceive();
                        }
                    }
                    else
                        RaiseDisconnect();
                }

                if (NextConnectionLoop == false)
                    break;
            }
        }

        public void CloseServer()
        {
            RTSPRequest RC = null;
            RTSPResponse RP = null;

            // Console.WriteLine("RTSP Closing")
            if ((iStatus != enumStatus.None) &&
                (iStatus != enumStatus.TEARDOWN) &&
                (iStatus != enumStatus.Closing))
            {
                bool IsConnected = false;

                iStatus = enumStatus.Closing;

                iIsBeginReceived = false;
                iLastRC = null;

                if (iRegControlPort != null)
                    iRegControlPort.Clear();

                if (iRegStreamPort != null)
                    iRegStreamPort.Clear();

                StopRTCPLoop();

                if (iBaseSender != null)
                {
                    try
                    {
                        if (iBaseSender.GetSourceTCP.Connected)
                            IsConnected = true;
                    }
                    catch (Exception ex)
                    {
                    }

                    if (IsConnected)
                    {
                        iStatus = enumStatus.TEARDOWN;

                        try
                        {
                            RC = iBaseSender.CreateRequest("TEARDOWN", iURL);

                            if (iSessionID != string.Empty)
                                RC.Header.Set("Session", iSessionID);

                            if (iIsNeedAuth)
                            {
                                if (iRTSPAuth != null)
                                    RC.Header.Set("Authorization", iRTSPAuth.ToHeaderString(iURL, RC.Method));
                            }

                            RTSPCmdSendBefore?.Invoke(this, RC);

                            try { iBaseSender.WriteRequest(RC); }
                            catch (Exception ex) { IsConnected = false; }
                        }
                        catch (Exception ex)
                        {
                        }
                    }

                    if (iStreamList != null)
                    {
                        byte[] RTCPGoodbye = BuildRTCPGoodBye();
                        RTSPClientStream[] SCArray = null;

                        lock (iStreamList)
                        {
                            SCArray = iStreamList.ToArray();
                        }

                        if (SCArray != null)
                        {
                            foreach (RTSPClientStream EachSC in SCArray)
                            {
                                if (iProtocolType == enumProtocolType.RTPOverUDP)
                                {
                                    EachSC.StreamFrame -= UDP_StreamFrame;
                                    EachSC.ControlFrame -= UDP_ControlFrame;
                                }

                                if (IsConnected)
                                {
                                    try
                                    {
                                        if ((iRTCPControlType == enumRTCPControlType.Auto) || (iRTCPControlType == enumRTCPControlType.Always))
                                        {
                                            // Send RTCP GoodBye
                                            byte[] SendBuffer = null;

                                            switch (EachSC.PortType)
                                            {
                                                case RTSPClientStream.enumPortType.RTPOverTCP:
                                                    SendBuffer = (byte[])Array.CreateInstance(typeof(byte), RTCPGoodbye.Length + 4);
                                                    SendBuffer[0] = 0x24;
                                                    SendBuffer[1] = (byte)EachSC.ControlPort;
                                                    SendBuffer[2] = (byte)(RTCPGoodbye.Length / 256);
                                                    SendBuffer[3] = (byte)(RTCPGoodbye.Length % 256);
                                                    Array.Copy(RTCPGoodbye, 0, SendBuffer, 4, RTCPGoodbye.Length);

                                                    try
                                                    {
                                                        if (iBaseSender != null)
                                                        {
                                                            if (iBaseSender.GetSourceTCP != null)
                                                            {
                                                                if (iBaseSender.GetSourceTCP.Connected)
                                                                    iBaseSender.GetSourceTCP.Client.Send(SendBuffer);// 網路線拔掉後, 此處可能會等待一段時間
                                                            }
                                                        }
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        IsConnected = false;
                                                    }

                                                    break;
                                                case RTSPClientStream.enumPortType.RTPOverUDP:
                                                    try { EachSC.SendControl(RTCPGoodbye); }
                                                    catch (Exception ex) { IsConnected = false; }

                                                    break;
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                    }
                                }

                                try { EachSC.Close(); }
                                catch (Exception ex) { }
                            }

                            if (iStreamList != null)
                            {
                                lock (iStreamList)
                                {
                                    iStreamList.Clear();
                                }
                            }
                        }
                    }
                }

                if (iBaseRecv != null)
                {
                    try { iBaseRecv.CloseServer(); }
                    catch (Exception ex) { }

                    if (iBaseRecv != null)
                    {
                        try { iBaseRecv.Dispose(); }
                        catch (Exception ex) { }
                    }
                }

                if (iBaseSender != null)
                {
                    try { iBaseSender.CloseServer(); }
                    catch (Exception ex) { }

                    if (iBaseSender != null)
                    {
                        try { iBaseSender.Dispose(); }
                        catch (Exception ex) { }
                    }
                }

                iStatus = enumStatus.None;

                iBaseRecv = null;
                iBaseSender = null;
                iBuffer = null;
            }
        }

        private string GetRequestURI(string prefix = "rtsp")
        {
            if (iHostURI)
            {
                System.Uri RetURI = new System.Uri(iURL);

                if (RetURI.Scheme.Trim().ToUpper() != prefix.ToUpper())
                    return prefix + "://" + RetURI.Host + ":" + RetURI.Port + RetURI.PathAndQuery;
                else
                    return iURL;
            }
            else
                return new System.Uri(iURL).PathAndQuery;
        }

        private byte[] BuildRTCPGoodBye()
        {
            // Goodbye
            return new byte[] { 0x81, 0xCB, 0, 1, 0, 0, 0x20, 0xC8 };
        }

        private byte[] BuildRTCPReceiverReport(RTP LastRTP, byte[] LastSRTimestamp)
        {
            if (LastRTP != null)
                return BuildRTCPReceiverReport(LastRTP.SSRC, LastRTP.SequenceNumber, LastSRTimestamp);
            else
                return BuildRTCPReceiverReport(0, 0, null);
        }

        private byte[] BuildRTCPReceiverReport(uint SSRC, ushort SequenceNumber, byte[] LastSRTimestamp)
        {
            List<byte> iList = new List<byte>();

            // RR
            if ((SSRC == 0) & (SequenceNumber == 0))
                iList.AddRange(new byte[] { 0x80, 0xC9, 0, 1, 0, 0, 0x20, 0xC8 });
            else
            {
                iList.AddRange(new byte[] { 0x81, 0xC9, 0, 7, 0, 0, 0x20, 0xC8 });
                // SSRC Content
                iList.AddRange(System.BitConverter.GetBytes(SSRC));
                iList.AddRange(new byte[] { 0, 0, 0, 0 });
                // Extension Sequence
                iList.AddRange(new byte[] { 0, 0 });
                iList.Add((byte)(SequenceNumber / 256));
                iList.Add((byte)(SequenceNumber % 256));
                // Jitter
                iList.AddRange(new byte[] { 0, 0, 0, 11 });

                // Last SR Timestamp
                if (LastSRTimestamp != null)
                    iList.AddRange(LastSRTimestamp);
                else
                    iList.AddRange(new byte[] { 0, 0, 0, 0 });

                // Last SR Delay 
                iList.AddRange(new byte[] { 0, 0, 0, 0 });
            }

            // Source Desc
            iList.AddRange(new byte[] { 0x81, 0xCA, 0x0, 0x4, 0, 0, 0x20, 0xC8, 1, 6, 0x73, 0x65, 0x72, 0x76, 0x65, 0x72, 0, 0, 0, 0 });

            return iList.ToArray();
        }

        private void BeginRTPReceive()
        {
            StartRTCPLoop();

            iIsBeginReceived = true;

            if (iBuffer == null)
                iBuffer = (byte[])Array.CreateInstance(typeof(byte), iBufferSize + 100);

            switch (iProtocolType)
            {
                case enumProtocolType.RTPOverUDP:
                    foreach (RTSPClientStream EachSC in iStreamList)
                    {
                        EachSC.StreamFrame += UDP_StreamFrame;
                        EachSC.ControlFrame += UDP_ControlFrame;

                        EachSC.BeginReceive();
                    }

                    break;
                default:
                    if (iBaseRecv != null)
                    {
                        if (iBaseRecv.GetSourceTCP != null)
                            TCPBeginReceive();
                    }

                    break;
            }
        }

        private void UDP_StreamFrame(RTSPClientStream sender, byte[] Data)
        {
            RTP RTP = new RTP(Data);

            iLastReceivedRTP = new RTP(RTP.ToByteArray());
            RTPStreamFrame?.Invoke(this, sender.StreamPort, RTP);

            RTP = null;
        }

        private void UDP_ControlFrame(RTSPClientStream sender, byte[] Data)
        {
            List<RTCP> iRTCPList = new List<RTCP>();
            int iDataOffset = 0;
            RTCP RTCP = null;

            // 計算 RTCP 長度
            while (true)
            {
                int RTCPLength;

                if (iDataOffset >= iFrameRawData.Count)
                    break;

                RTCPLength = (iFrameRawData[iDataOffset + 2] * 256 + iFrameRawData[iDataOffset + 3] * 4) + 4;
                if ((RTCPLength > 0) && (RTCPLength <= 65535))
                {
                    if ((Data.Length - iDataOffset) >= RTCPLength)
                    {
                        byte[] RTCPContent = null;
                        RTCP RTCPFrame = null;
                        RTCP.RTCPSenderReport RTCPSR = null;

                        RTCPContent = (byte[])Array.CreateInstance(typeof(byte), RTCPLength);
                        iFrameRawData.CopyTo(iDataOffset, RTCPContent, 0, RTCPContent.Length);

                        RTCPFrame = new RTCP(RTCPContent);
                        RTCPSR = ProcessRTCPSenderReport(RTCPFrame);
                        if (RTCPSR != null)
                        {
                            sender.iLastSenderReport = RTCPSR;
                            sender.iRTCPRecv = true;
                        }

                        iRTCPList.Add(RTCPFrame);
                    }

                    iDataOffset += RTCPLength;
                }
                else
                    break;
            }

            iRTCPServerReport = true;

            RTPControlFrame?.Invoke(this, sender.ControlPort, iRTCPList.ToArray());
        }


        private void RaiseDisconnect()
        {
            Console.WriteLine("RTSP Disconneced");

            this.CloseServer();

            RTSPDisconnect?.Invoke(this);
        }

        private void TCPBeginReceive()
        {
            if (iBaseRecv != null)
            {
                if (iBaseRecv.GetSourceTCP != null)
                {
                    try { iBaseRecv.GetSourceTCP.Client.BeginReceive(iBuffer, 0, iBufferSize, System.Net.Sockets.SocketFlags.None, iTCP_EndReceive, null); }
                    catch (Exception ex) { RaiseDisconnect(); /* 呼叫失敗 */ }
                }
            }
        }

        private void iTCP_EndReceive(System.IAsyncResult ar)
        {
            int ReceiveBytes = 0;

            if (iIsBeginReceived)
            {
                if (iBaseRecv != null)
                {
                    try { ReceiveBytes = iBaseRecv.GetSourceTCP.Client.EndReceive(ar); }
                    catch (Exception ex) { }
                }
            }

            if (ReceiveBytes > 0)
            {
                // First 4 Bytes:
                // MagicCode, Channel, Length (2)
                if ((iBuffer != null) && (iFrameRawData != null))
                {
                    iInReceiveProcess = true;
                    iFrameRawData.AddRange(iBuffer, 0, ReceiveBytes);

                    try { ReceiveProcess(); }
                    catch (Exception ex) { Console.WriteLine(ex.ToString()); }

                    iInReceiveProcess = false;
                }

                TCPBeginReceive();
            }
            else
                RaiseDisconnect();
        }

        private void ReceiveProcess()
        {
            int LoopIndex;

            if (iProtocolType == enumProtocolType.HttpPush)
            {
                // Find Boundary
                for (LoopIndex = 1; LoopIndex <= 100; LoopIndex++)
                {
                    if ((iFrameRawData.Count <= 0) || (iStatus == enumStatus.None))
                        break;

                    if (iBaseRecv != null)
                    {
                        bool Connected = false;

                        try { Connected = iBaseRecv.GetSourceTCP.Connected; }
                        catch (Exception ex) { }

                        if (Connected == false)
                            break;
                    }

                    if (iWaitingBoundary)
                    {
                        if (iFrameRawData.Count >= iHttpBoundary.Length)
                        {
                            bool PosBoundary = false;

                            for (int I = 0; I <= (iFrameRawData.Count - iHttpBoundary.Count()); I++)
                            {
                                bool Found = true;

                                if (iStatus == enumStatus.None)
                                    break;

                                for (var J = 0; J <= iHttpBoundary.Count() - 1; J++)
                                {
                                    if ((iFrameRawData.iInternalArray[I + J] != iHttpBoundary[J]) || (iStatus == enumStatus.None))
                                    {
                                        Found = false;
                                        break;
                                    }
                                }

                                if (Found)
                                {
                                    if (I > 0)
                                        iFrameRawData.RemoveRange(0, I);

                                    PosBoundary = true;
                                    break;
                                }
                            }

                            if (PosBoundary)
                            {
                                // Find Double Line
                                HttpSpliter HS = new HttpSpliter();
                                int HeaderLength = -1;
                                int BodyIndex = -1;

                                HeaderLength = HS.FindDoubleLineBreak(iFrameRawData, ref BodyIndex);

                                if ((HeaderLength != -1) &&
                                    (BodyIndex <= (iFrameRawData.Count - 1)) &&
                                    (HeaderLength <= (iFrameRawData.Count - 1)))
                                {
                                    byte[] HeadBytes = null;
                                    string HeadStr;

                                    iWaitingBoundary = false;

                                    if (iBoundaryHeader == null)
                                        iBoundaryHeader = new RTSPHeaderSet();
                                    else
                                        iBoundaryHeader.Clear();

                                    HeadBytes = (byte[])Array.CreateInstance(typeof(byte), HeaderLength);
                                    iFrameRawData.CopyTo(0, HeadBytes, 0, HeadBytes.Length);
                                    iFrameRawData.RemoveRange(0, BodyIndex);

                                    HeadStr = System.Text.Encoding.UTF8.GetString(HeadBytes);

                                    foreach (string EachStr in HS.SplitContent(HeadStr))
                                    {
                                        int TmpIndex;

                                        if (EachStr != string.Empty)
                                        {
                                            TmpIndex = EachStr.IndexOf(":");

                                            if (TmpIndex != -1)
                                            {
                                                string Cmd;
                                                string Value;

                                                Cmd = EachStr.Substring(0, TmpIndex).Trim();
                                                Value = EachStr.Substring(TmpIndex + 1).Trim();

                                                iBoundaryHeader.Set(Cmd, Value);
                                            }
                                        }
                                    }

                                    if (iBoundaryHeader["Content-Length"] != null)
                                    {
                                        if (System.Int32.TryParse(iBoundaryHeader["Content-Length"], out iWaitContentLength) == false)
                                            iWaitContentLength = -1;
                                    }
                                    else
                                        iWaitContentLength = -1;
                                }
                                else
                                    break;
                            }
                            else
                            {
                                if ((iFrameRawData.Count - iHttpBoundary.Count()) > 0)
                                    iFrameRawData.RemoveRange(0, iFrameRawData.Count - iHttpBoundary.Count());

                                break;
                            }
                        }
                        else
                            break;
                    }
                    else if ((iWaitContentLength != -1))
                    {
                        if (iFrameRawData.Count >= iWaitContentLength)
                        {
                            HttpPushFrame HPF = null;

                            HPF = new HttpPushFrame();
                            HPF.Header = iBoundaryHeader;
                            HPF.Body = iFrameRawData;
                            HPF.BodyOffset = 0;
                            HPF.BodyLength = iWaitContentLength;

                            HttpPushFrame?.Invoke(this, HPF);

                            HPF = null;

                            iFrameRawData.RemoveRange(0, iWaitContentLength);

                            iBoundaryHeader.Clear();
                            iBoundaryHeader = null;

                            iWaitContentLength = -1;
                            iWaitingBoundary = true;
                        }
                        else
                            break;
                    }
                    else
                    {
                        // -1 ?? no support
                        // Find next boundary

                        if (iFrameRawData.Count >= iHttpBoundary.Length)
                        {
                            bool NextBoundary = false;
                            int SearchBoundaryIndex = 0;

                            if (iFrameRawData.Count < iLastBoundarySearchIndex)
                                SearchBoundaryIndex = 0;
                            else
                                SearchBoundaryIndex = Math.Max((iLastBoundarySearchIndex - iHttpBoundary.Length), 0);

                            for (int I = (iFrameRawData.Count - 1); I >= SearchBoundaryIndex; I -= iHttpBoundary.Length)
                            {
                                bool Found = false;
                                int p = I;

                                if (iStatus == enumStatus.None)
                                    break;

                                if (iBoundaryHash.Contains(iFrameRawData[I]))
                                {
                                    for (int J = (I - 1); J >= SearchBoundaryIndex; J--)
                                    {
                                        if (iBoundaryHash.Contains(iFrameRawData[J]) == false)
                                        {
                                            // 該字元不在 BoundaryHash 列表內
                                            if ((J + iHttpBoundary.Length) <= (iFrameRawData.Count - 1))
                                            {
                                                Found = true;
                                                for (int K = 0; K <= (iHttpBoundary.Length - 1); K++)
                                                {
                                                    if (iFrameRawData[J + K + 1] != iHttpBoundary[K])
                                                    {
                                                        Found = false;
                                                        break;
                                                    }
                                                }

                                                if (Found == true)
                                                    p = J + 1;
                                            }

                                            break;
                                        }
                                    }
                                }

                                // 先做 "-" 號定位
                                // If iFrameRawData(I + 1) = &H2D Then
                                // ' 找尋另外一個 "-" 的位置
                                // If iFrameRawData(I) <> &H2D Then
                                // p = I + 1
                                // End If

                                // For J = 0 To iHttpBoundary.Count - 1
                                // If ((iFrameRawData(p + J + 2) <> iHttpBoundary(J)) And (iFrameRawData(p + J) <> iHttpBoundary(J))) Or _
                                // (iStatus = enumStatus.None) Then
                                // Found = False
                                // Exit For
                                // End If
                                // Next
                                // Else
                                // Found = False
                                // End If

                                if (Found)
                                {
                                    iWaitContentLength = p;
                                    NextBoundary = true;
                                    break;
                                }
                            }

                            if (NextBoundary)
                            {
                                iLastBoundarySearchIndex = 0;

                                if (iWaitContentLength < 1000000)
                                {
                                    HttpPushFrame HPF = null;

                                    if (iWaitContentLength >= 2)
                                    {
                                        if ((iFrameRawData[iWaitContentLength - 2] == 13) && (iFrameRawData[iWaitContentLength - 1] == 10))
                                            iWaitContentLength -= 2;
                                    }

                                    HPF = new HttpPushFrame();
                                    HPF.Header = iBoundaryHeader;
                                    HPF.Body = iFrameRawData;
                                    HPF.BodyOffset = 0;
                                    HPF.BodyLength = iWaitContentLength;

                                    HttpPushFrame?.Invoke(this, HPF);

                                    HPF = null;

                                    iFrameRawData.RemoveRange(0, iWaitContentLength);

                                    iBoundaryHeader.Clear();
                                    iBoundaryHeader = null;

                                    iWaitContentLength = -1;
                                    iWaitingBoundary = true;
                                }
                                else
                                {
                                    Console.WriteLine("Boundary length too big:" + iWaitContentLength);
                                    iFrameRawData.Clear();
                                }
                            }
                            else
                            {
                                iLastBoundarySearchIndex = iFrameRawData.Count;
                                break;
                            }
                        }
                        else
                            break;
                    }
                }

                if (LoopIndex >= 100)
                    iFrameRawData.Clear();
            }
            else if (iProtocolType == enumProtocolType.HttpRaw)
            {
                // Find Boundary
                if ((iFrameRawData.Count > 0) && (iStatus != enumStatus.None))
                {
                    if (iBaseRecv != null)
                    {
                        bool Connected = false;

                        try { Connected = iBaseRecv.GetSourceTCP.Connected; }
                        catch (Exception ex) { }

                        if (Connected)
                        {
                            HttpPushFrame HPF = null;

                            HPF = new HttpPushFrame();
                            HPF.Header = iBoundaryHeader;
                            HPF.Body = iFrameRawData;
                            HPF.BodyOffset = 0;
                            HPF.BodyLength = iFrameRawData.Count;

                            HttpPushFrame?.Invoke(this, HPF);

                            HPF = null;

                            iFrameRawData.Clear();
                        }
                    }
                }
            }
            else if (iFrameRawData.Count >= 4)
            {
                for (LoopIndex = 1; LoopIndex <= 100; LoopIndex++)
                {
                    if (iFrameRawData.Count >= 4)
                    {
                        if (iFrameRawData[0] == 0x24)
                        {
                            // Magic Code
                            byte Channel;
                            int DataLength;

                            Channel = iFrameRawData[1];
                            DataLength = iFrameRawData[2] * 256 + iFrameRawData[3];
                            iLastDataLength = DataLength;

                            if ((iFrameRawData.Count >= (DataLength + 4)))
                            {
                                if (DataLength >= 16)
                                {
                                    if (iRegStreamPort.ContainsKey(Channel))
                                    {
                                        RTSPClientStream CS = null;
                                        RTP RTP = new RTP(iFrameRawData, 4, DataLength);

                                        iLastSeqNumber = RTP.SequenceNumber;

                                        try { CS = iRegStreamPort[Channel]; }
                                        catch (Exception ex) { }

                                        if (CS != null)
                                        {
                                            CS.iLastSequenceNumber = RTP.SequenceNumber;
                                            CS.iLastSSRC = RTP.SSRC;
                                        }

                                        iLastReceivedRTP = new RTP(RTP.ToByteArray());
                                        RTPStreamFrame?.Invoke(this, Channel, RTP);

                                        RTP = null;
                                    }
                                    else if (iRegControlPort.ContainsKey(Channel))
                                    {
                                        List<RTCP> iRTCPList = new List<RTCP>();
                                        int iDataOffset = 4;
                                        RTSPClientStream RTSPStm = null;

                                        RTSPStm = iRegControlPort[Channel];

                                        // 計算 RTCP 長度
                                        for (int I = 1; I <= 100; I++)
                                        {
                                            int RTCPLength;

                                            if (iDataOffset + 4 >= iFrameRawData.Count)
                                                break;

                                            RTCPLength = (iFrameRawData[iDataOffset + 2] * 256 + iFrameRawData[iDataOffset + 3] * 4) + 4;

                                            if ((RTCPLength > 0) && (RTCPLength <= 65535))
                                            {
                                                if ((DataLength - iDataOffset) >= RTCPLength)
                                                {
                                                    byte[] RTCPContent = null;
                                                    RTCP RTCPFrame = null;
                                                    RTCP.RTCPSenderReport RTCPSR = null;

                                                    RTCPContent = (byte[])Array.CreateInstance(typeof(byte), RTCPLength);
                                                    iFrameRawData.CopyTo(iDataOffset, RTCPContent, 0, RTCPContent.Length);

                                                    RTCPFrame = new RTCP(RTCPContent);

                                                    RTCPSR = ProcessRTCPSenderReport(RTCPFrame);
                                                    if (RTCPSR != null)
                                                    {
                                                        if (RTSPStm != null)
                                                        {
                                                            RTSPStm.iLastSenderReport = RTCPSR;
                                                            RTSPStm.iRTCPRecv = true;
                                                        }
                                                    }

                                                    iRTCPList.Add(RTCPFrame);
                                                }

                                                iDataOffset += RTCPLength;
                                            }
                                            else
                                                break;
                                        }

                                        iRTCPServerReport = true;

                                        RTPControlFrame?.Invoke(this, Channel, iRTCPList.ToArray());
                                    }
                                }

                                // Console.WriteLine(Channel & ":" & DataLength + 4 & "/" & iFrameRawData.Count)
                                iFrameRawData.RemoveRange(0, DataLength + 4);
                            }
                            else
                                break;
                        }
                        else
                        {
                            bool IsPacketLost = false;

                            if ((iFrameRawData[0] == 0x52) &&
                                (iFrameRawData[1] == 0x54) &&
                                (iFrameRawData[2] == 0x53) &&
                                (iFrameRawData[3] == 0x50))
                            {
                                HttpSpliter HS = new HttpSpliter();
                                int BodyIndex = -1;

                                if (HS.FindDoubleLineBreak(iFrameRawData, ref BodyIndex) != -1)
                                {
                                    byte[] RTSPHead = null;

                                    RTSPHead = (byte[])Array.CreateInstance(typeof(byte), BodyIndex);

                                    iFrameRawData.CopyTo(0, RTSPHead, 0, BodyIndex);
                                    iFrameRawData.RemoveRange(0, BodyIndex);
                                }
                            }
                            else
                                IsPacketLost = true;

                            if (IsPacketLost)
                            {
                                // Packet lost
                                System.Net.IPEndPoint RemoteEP = null;
                                string RemoteIP = string.Empty;

                                try { RemoteEP = (System.Net.IPEndPoint)iBaseRecv.GetSourceTCP.Client.RemoteEndPoint; }
                                catch (Exception ex) { }

                                if (RemoteEP != null)
                                    RemoteIP = RemoteEP.Address.ToString();

                                Console.WriteLine("(" + RemoteIP + ") RTP Length wrong (last packet length: " + iLastDataLength + ", Seq:" + iLastSeqNumber + "), looking for next magic code(0x24)...");

                                if (iRTPLostSearch == enumRTPLostSearch.SearchNext)
                                {
                                    bool FoundNextCode = false;
                                    // ERROR

                                    for (int I = 0; I <= iFrameRawData.Count - 2; I++)
                                    {
                                        bool IsFramePacket = false;

                                        if (iFrameRawData[I] == 0x24)
                                        {
                                            if (iRegStreamPort.ContainsKey(iFrameRawData[I + 1]))
                                                IsFramePacket = true;
                                            else if (iRegControlPort.ContainsKey(iFrameRawData[I + 1]))
                                                IsFramePacket = true;

                                            if (IsFramePacket)
                                            {
                                                FoundNextCode = true;

                                                if (iLastRC != null)
                                                {
                                                    RTSPResponse RP = new RTSPResponse();
                                                    byte[] Content = null;

                                                    Content = (byte[])Array.CreateInstance(typeof(byte), I);

                                                    iFrameRawData.CopyTo(0, Content, 0, Content.Length);

                                                    if (RP.Parsing(Content))
                                                    {
                                                        RTSPCmdSendResponse?.Invoke(this, iLastRC, RP);

                                                        iLastRC = null;
                                                    }
                                                }

                                                iFrameRawData.RemoveRange(0, I);

                                                break;
                                            }
                                        }
                                    }

                                    if (FoundNextCode == false)
                                    {
                                        if (iLastRC != null)
                                        {
                                            RTSPResponse RP = new RTSPResponse();
                                            byte[] Content = null;

                                            Content = iFrameRawData.ToArray();

                                            if (RP.Parsing(Content))
                                            {
                                                RTSPCmdSendResponse?.Invoke(this, iLastRC, RP);

                                                iLastRC = null;
                                            }
                                        }

                                        iFrameRawData.Clear();

                                        break;
                                    }
                                }
                                else if (iRTPLostSearch == enumRTPLostSearch.WaitNext)
                                {
                                    iFrameRawData.Clear();

                                    break;
                                }
                            }
                        }
                    }
                    else
                        break;
                }

                if (LoopIndex >= 100)
                    iFrameRawData.Clear();
            }
        }

        private void InitProtocol()
        {
            Random r = new Random();

            iRTPSeq = r.Next(10, 10000);
        }

        public RTSPClientStream AddRTSPClientStream(string Base_URL)
        {
            RTSPClientStream SC = null;

            switch (iProtocolType)
            {
                case enumProtocolType.RTPOverUDP:
                    SC = new RTSPClientStream(Base_URL, RTSPClientStream.enumPortType.RTPOverUDP, 0, 0);

                    break;
                default:
                    SC = new RTSPClientStream(Base_URL, RTSPClientStream.enumPortType.RTPOverTCP, iBasePortIndex, iBasePortIndex + 1);

                    break;
            }

            iStreamList.Add(SC);

            iBasePortIndex += 2;

            return SC;
        }

        private int GetLocalPort(System.Net.Sockets.Socket So)
        {
            System.Net.IPEndPoint IPEP = null;
            int RetValue = 0;

            if (So != null)
            {
                IPEP = (System.Net.IPEndPoint)So.LocalEndPoint;
                if (IPEP != null)
                    RetValue = IPEP.Port;
            }

            return RetValue;
        }

        private void StartRTCPLoop()
        {
            if (iRTCPLoopThread != null)
            {
                try { iRTCPLoopThread.Join(1000); }
                catch (Exception ex) { }

                if (iRTCPLoopThread != null)
                    iRTCPLoopThread.Abort();

                iRTCPLoopThread = null;
            }

            iRTCPLoopExit = false;

            iRTCPLoopThread = new System.Threading.Thread(RTCP_LoopMonitor);
            iRTCPLoopThread.IsBackground = true;
            iRTCPLoopThread.Start();
        }

        private void StopRTCPLoop()
        {
            iRTCPLoopExit = true;

            if (iRTCPLoopThread != null)
            {
                try { iRTCPLoopThread.Join(1000); }
                catch (Exception ex) { }

                if (iRTCPLoopThread != null)
                    iRTCPLoopThread.Abort();

                iRTCPLoopThread = null;
            }
        }

        private RTCP.RTCPSenderReport ProcessRTCPSenderReport(RTCP RTCP)
        {
            RTCP.RTCPSenderReport RetValue = null;

            if (RTCP.PacketType == RTCP.enumPacketType.SenderReport)
            {
                try { RetValue = (RTCP.RTCPSenderReport)RTCP.GetBody(); }
                catch (Exception ex) { }
            }

            return RetValue;
        }

        private void RTCP_LoopMonitor()
        {
            while (true)
            {
                if (disposedValue || iRTCPLoopExit)
                    break;

                try { RTCP_Loop(); }
                catch (Exception ex) { }

                System.Threading.Thread.Sleep(10);
            }

            iRTCPLoopThread = null;
        }

        private void RTCP_Loop()
        {
            DateTime WaitTimer;
            DateTime EntryDate = DateTime.Now;

            while (true)
            {
                if (disposedValue || iRTCPLoopExit)
                    break;

                if ((iProtocolType != enumProtocolType.HttpPush) && (iProtocolType != enumProtocolType.HttpRaw))
                {
                    if ((iRTCPControlType != enumRTCPControlType.Disable))
                    {
                        RTSPClientStream[] SCList = null;

                        lock (iStreamList)
                        {
                            SCList = iStreamList.ToArray();
                        }

                        foreach (RTSPClientStream SC in SCList)
                        {
                            bool AllowSendRTCP = false;
                            byte[] RTCPContent = null;
                            byte[] LastSRTimestamp = null;

                            switch (iRTCPControlType)
                            {
                                case enumRTCPControlType.Auto:
                                    if (SC.iRTCPRecv)
                                    {
                                        SC.iRTCPRecv = false;
                                        AllowSendRTCP = true;
                                    }

                                    break;
                                case enumRTCPControlType.Always:
                                    if ((System.Convert.ToInt32(DateTime.Now.Subtract(EntryDate).TotalSeconds) % 2) == 0)
                                        AllowSendRTCP = true;

                                    break;
                            }

                            if (AllowSendRTCP)
                            {
                                if (SC.iLastSenderReport != null)
                                {
                                    LastSRTimestamp = (byte[])Array.CreateInstance(typeof(byte), 4);
                                    Array.Copy(SC.iLastSenderReport.TimestampMSWBytes, 2, LastSRTimestamp, 0, 2);
                                    Array.Copy(SC.iLastSenderReport.TimestampLSWBytes, 0, LastSRTimestamp, 2, 2);
                                }

                                // If IsNothing(iLastReceivedRTP) Then
                                RTCPContent = BuildRTCPReceiverReport(SC.iLastSSRC, SC.iLastSequenceNumber, LastSRTimestamp);
                                // Else
                                // RTCPContent = BuildRTCPReceiverReport(iLastReceivedRTP, Nothing)
                                // End If

                                // 暫時不支援 Type2
                                switch (iProtocolType)
                                {
                                    case enumProtocolType.RTSPOverHTTP_B64:
                                        if (iBaseSender != null)
                                        {
                                            try
                                            {
                                                if (iBaseSender.GetSourceTCP != null)
                                                {
                                                    if (iBaseSender.GetSourceTCP.Connected)
                                                    {
                                                        byte[] SendBuffer = null;
                                                        string B64String;

                                                        SendBuffer = (byte[])Array.CreateInstance(typeof(byte), RTCPContent.Length + 4);
                                                        SendBuffer[0] = 0x24;
                                                        SendBuffer[1] = (byte)SC.ControlPort;
                                                        SendBuffer[2] = (byte)(RTCPContent.Length / 256);
                                                        SendBuffer[3] = (byte)(RTCPContent.Length % 256);

                                                        Array.Copy(RTCPContent, 0, SendBuffer, 4, RTCPContent.Length);

                                                        B64String = System.Convert.ToBase64String(SendBuffer);

                                                        iBaseSender.GetSourceTCP.Client.Send(System.Text.Encoding.UTF8.GetBytes(B64String));
                                                    }
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                            }
                                        }

                                        break;
                                    case enumProtocolType.RTPOverTCP:
                                    case enumProtocolType.RTSPOverHTTP_Text:
                                        if (iBaseSender != null)
                                        {
                                            try
                                            {
                                                if (iBaseSender.GetSourceTCP != null)
                                                {
                                                    if (iBaseSender.GetSourceTCP.Connected)
                                                    {
                                                        byte[] SendBuffer = null;

                                                        SendBuffer = (byte[])Array.CreateInstance(typeof(byte), RTCPContent.Length + 4);
                                                        SendBuffer[0] = 0x24;
                                                        SendBuffer[1] = (byte)SC.ControlPort;
                                                        SendBuffer[2] = (byte)(RTCPContent.Length / 256);
                                                        SendBuffer[3] = (byte)(RTCPContent.Length % 256);

                                                        Array.Copy(RTCPContent, 0, SendBuffer, 4, RTCPContent.Length);

                                                        iBaseSender.GetSourceTCP.Client.Send(SendBuffer);
                                                    }
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                            }
                                        }

                                        break;
                                    case enumProtocolType.RTPOverUDP:
                                        try { SC.SendControl(RTCPContent); }
                                        catch (Exception ex) { }

                                        break;
                                }
                            }
                        }
                    }
                }

                WaitTimer = DateTime.Now;

                while (true)
                {
                    if (disposedValue || iRTCPLoopExit)
                        break;

                    if (DateTime.Now >= WaitTimer)
                    {
                        if (DateTime.Now.Subtract(WaitTimer).TotalMilliseconds >= 500)
                            break;
                    }
                    else
                        WaitTimer = DateTime.Now;

                    System.Threading.Thread.Sleep(10);
                }
            }
        }

        private bool disposedValue = false;
        private System.Collections.ArrayList disposedSyncObj = new System.Collections.ArrayList();

        // IDisposable
        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposedValue)
            {
                if (disposing)
                {
                    if (disposedSyncObj != null)
                    {
                        lock (disposedSyncObj)
                        {
                            StopRTCPLoop();

                            if (iBaseRecv != null)
                            {
                                try { iBaseRecv.Dispose(); }
                                catch (Exception ex) { }

                                iBaseRecv = null;
                            }

                            if (iBaseSender != null)
                            {
                                try { iBaseSender.Dispose(); }
                                catch (Exception ex) { }

                                iBaseSender = null;
                            }

                            // wait receive complete
                            if (iInReceiveProcess)
                            {
                                for (int I = 1; I <= 3; I++)
                                {
                                    if (iInReceiveProcess == false)
                                        break;

                                    System.Threading.Thread.Sleep(500);
                                }
                            }

                            if (iRegStreamPort != null)
                            {
                                lock (iRegStreamPort)
                                {
                                    foreach (RTSPClientStream EachCS in iRegStreamPort.Values)
                                    {
                                        try { EachCS.Dispose(); }
                                        catch (Exception ex) { }
                                    }

                                    iRegStreamPort.Clear();
                                    iRegStreamPort = null;
                                }
                            }

                            if (iRegControlPort != null)
                            {
                                lock (iRegControlPort)
                                {
                                    foreach (RTSPClientStream EachCS in iRegControlPort.Values)
                                    {
                                        try { EachCS.Dispose(); }
                                        catch (Exception ex) { }
                                    }

                                    iRegControlPort.Clear();
                                    iRegControlPort = null;
                                }
                            }

                            if (iStreamList != null)
                            {
                                iStreamList.Clear();
                                iStreamList = null;
                            }
                        }
                    }
                }
            }

            this.disposedValue = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }

    public class RTSPException : Exception
    {
        private RTSPRequest iRequest;
        private RTSPResponse iResponse;

        public RTSPRequest Request
        {
            get { return iRequest; }
        }

        public RTSPResponse Response
        {
            get { return iResponse; }
        }

        public RTSPException(string Msg, RTSPRequest RC, RTSPResponse RP) : base(Msg)
        {
            iRequest = RC;
            iResponse = RP;
        }
    }

    public class RTSPClientStream : IDisposable
    {
        private System.Net.Sockets.Socket iStreamUDP;
        private System.Net.Sockets.Socket iControlUDP;

        private int iStreamBufferSize;
        private int iControlBufferSize;
        private byte[] iStreamBuffer = null;
        private byte[] iControlBuffer = null;

        private int iStreamPort = 0;
        private int iControlPort = 1;

        private string iSETUP_URL;
        // Friend iLastRTPContent() As Byte
        internal uint iLastSSRC;
        internal ushort iLastSequenceNumber;
        internal RTCP.RTCPSenderReport iLastSenderReport;
        internal bool iRTCPRecv = false;

        private enumPortType iPortType = enumPortType.RTPOverTCP;

        public delegate void StreamFrameEventHandler(RTSPClientStream sender, byte[] Data);
        public event StreamFrameEventHandler StreamFrame;

        public delegate void ControlFrameEventHandler(RTSPClientStream sender, byte[] Data);
        public event ControlFrameEventHandler ControlFrame;


        public enum enumPortType
        {
            RTPOverTCP = 0,
            RTPOverUDP = 1
        }

        public int StreamPort
        {
            get
            {
                int RetValue = 0;

                switch (iPortType)
                {
                    case enumPortType.RTPOverTCP:
                        RetValue = iStreamPort;

                        break;
                    case enumPortType.RTPOverUDP:
                        System.Net.IPEndPoint IPEndPoint = (System.Net.IPEndPoint)iStreamUDP.LocalEndPoint;

                        RetValue = IPEndPoint.Port;

                        break;
                }

                return RetValue;
            }
            set
            {
                switch (iPortType)
                {
                    case enumPortType.RTPOverTCP:
                        iStreamPort = value;

                        break;
                }
            }
        }

        public int ControlPort
        {
            get
            {
                int RetValue = 0;

                switch (iPortType)
                {
                    case enumPortType.RTPOverTCP:
                        RetValue = iControlPort;

                        break;
                    case enumPortType.RTPOverUDP:
                        System.Net.IPEndPoint IPEndPoint = (System.Net.IPEndPoint)iControlUDP.LocalEndPoint;

                        RetValue = IPEndPoint.Port;

                        break;
                }

                return RetValue;
            }
            set
            {
                switch (iPortType)
                {
                    case enumPortType.RTPOverTCP:
                        iControlPort = value;

                        break;
                }
            }
        }

        public string SETUP_URL
        {
            get { return iSETUP_URL; }
            set { iSETUP_URL = value; }
        }

        public enumPortType PortType
        {
            get { return iPortType; }
        }

        public void SendStream(byte[] Data)
        {
            if (iStreamUDP != null)
                iStreamUDP.Send(Data, System.Net.Sockets.SocketFlags.None);
        }

        public void SendControl(byte[] Data)
        {
            if (iControlUDP != null)
                iControlUDP.Send(Data, System.Net.Sockets.SocketFlags.None);
        }

        private void UDPBeginReceiveStream()
        {
            if (iStreamUDP != null)
            {
                try { iStreamUDP.BeginReceive(iStreamBuffer, 0, iStreamBufferSize, System.Net.Sockets.SocketFlags.None, iStreamUDP_EndReceive, null); }
                catch (Exception ex) { }
            }
        }

        private void UDPBeginReceiveControl()
        {
            if (iControlUDP != null)
            {
                try { iControlUDP.BeginReceive(iControlBuffer, 0, iControlBufferSize, System.Net.Sockets.SocketFlags.None, iControlUDP_EndReceive, null); }
                catch (Exception ex) { }
            }
        }

        private void iStreamUDP_EndReceive(System.IAsyncResult ar)
        {
            int ReceiveBytes = 0;
            byte[] RecvBuff = null;

            if (iStreamUDP != null)
            {
                try { ReceiveBytes = iStreamUDP.EndReceive(ar); }
                catch (Exception ex) { }
            }

            if (ReceiveBytes > 0)
            {
                RecvBuff = (byte[])Array.CreateInstance(typeof(byte), ReceiveBytes);

                Array.Copy(iStreamBuffer, 0, RecvBuff, 0, RecvBuff.Length);

                StreamFrame?.Invoke(this, RecvBuff);
            }

            UDPBeginReceiveStream();
        }

        private void iControlUDP_EndReceive(System.IAsyncResult ar)
        {
            int ReceiveBytes = 0;
            byte[] RecvBuff = null;

            if (iControlUDP != null)
            {
                try { ReceiveBytes = iControlUDP.EndReceive(ar); }
                catch (Exception ex) { }
            }

            if (ReceiveBytes > 0)
            {
                RecvBuff = (byte[])Array.CreateInstance(typeof(byte), ReceiveBytes);

                Array.Copy(iControlBuffer, 0, RecvBuff, 0, RecvBuff.Length);

                ControlFrame?.Invoke(this, RecvBuff);
            }

            UDPBeginReceiveControl();
        }

        public void BeginReceive()
        {
            UDPBeginReceiveStream();
            UDPBeginReceiveControl();
        }

        public bool Connect(System.Net.IPAddress RemoteIP, int StreamPort, int ControlPort)
        {
            bool RetValue = false;

            if (iPortType == enumPortType.RTPOverUDP)
            {
                iStreamUDP.Connect(RemoteIP, StreamPort);
                iControlUDP.Connect(RemoteIP, ControlPort);

                RetValue = true;
            }

            return RetValue;
        }

        public void Close()
        {
            if (iStreamUDP != null)
            {
                try { iStreamUDP.Close(); }
                catch (Exception ex) { }
            }

            iStreamUDP = null;

            if (iControlUDP != null)
            {
                try { iControlUDP.Close(); }
                catch (Exception ex) { }
            }

            iControlUDP = null;
        }

        public RTSPClientStream(string BaseURL, enumPortType PortType, int BindStreamPort, int BindControlPort)
        {
            SETUP_URL = BaseURL;

            iPortType = PortType;

            iStreamPort = BindStreamPort;
            iControlPort = BindControlPort;

            switch (PortType)
            {
                case enumPortType.RTPOverTCP:
                    break;
                case enumPortType.RTPOverUDP:
                    iStreamUDP = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.InterNetwork, System.Net.Sockets.SocketType.Dgram, System.Net.Sockets.ProtocolType.Udp);
                    iControlUDP = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.InterNetwork, System.Net.Sockets.SocketType.Dgram, System.Net.Sockets.ProtocolType.Udp);

                    iStreamBufferSize = 81920;
                    iControlBufferSize = 8192;

                    iStreamUDP.SetSocketOption(System.Net.Sockets.SocketOptionLevel.Socket, System.Net.Sockets.SocketOptionName.ReceiveBuffer, iStreamBufferSize);
                    iControlUDP.SetSocketOption(System.Net.Sockets.SocketOptionLevel.Socket, System.Net.Sockets.SocketOptionName.ReceiveBuffer, iControlBufferSize);

                    iStreamUDP.Bind(new System.Net.IPEndPoint(System.Net.IPAddress.Any, iStreamPort));
                    iControlUDP.Bind(new System.Net.IPEndPoint(System.Net.IPAddress.Any, iControlPort));

                    iStreamUDP.EnableBroadcast = true;
                    iStreamUDP.DontFragment = true;
                    // iStreamUDP.Blocking = False


                    iStreamBuffer = (byte[])Array.CreateInstance(typeof(byte), iStreamBufferSize + 100);
                    iControlBuffer = (byte[])Array.CreateInstance(typeof(byte), iControlBufferSize + 100);

                    break;
            }
        }

        private bool disposedValue = false;

        // IDisposable
        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposedValue)
            {
                if (disposing)
                {
                    if (iStreamUDP != null)
                    {
                        try { iStreamUDP.Close(); }
                        catch (Exception ex) { }

                        iStreamUDP = null;
                    }

                    if (iControlUDP != null)
                    {
                        try { iControlUDP.Close(); }
                        catch (Exception ex) { }

                        iControlUDP = null;
                    }
                }
            }
            this.disposedValue = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
