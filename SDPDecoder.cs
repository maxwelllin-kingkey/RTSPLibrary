using System;
using System.Collections.Generic;

namespace RTSPLibrary
{
    public partial class SDPDecoder
    {
        public SDPContent ProcessSDP(string RequestURL, string S)
        {
            SDPContent RetValue = new SDPContent();
            string[] SDPArray = null;
            int MediaType = -1; // -1=not stream desc, 0=video, 1=audio, 2=other
            string TrackInfoPath = string.Empty;
            Uri SourceURI = null;
            HttpSpliter HP = new HttpSpliter();
            SDPContent.SDPMapInfo MI = null;

            if (RequestURL != null)
            {
                if (string.IsNullOrEmpty (RequestURL) == false)
                {
                    if (RequestURL.Substring(RequestURL.Length - 1, 1) != "/")
                        RequestURL += "/";

                    try { SourceURI = new Uri(RequestURL); }
                    catch (Exception ex) { }

                    RetValue.LocationBase = SourceURI;
                }
            }

            SDPArray = HP.SplitContent(S);
            foreach (string EachSDP in SDPArray)
            {
                int TmpIndex;
                string CmdStr;
                string CmdValue;
                string StringSDP;

                StringSDP = EachSDP.Replace("\r\n", string.Empty);

                TmpIndex = StringSDP.IndexOf("=");
                if (TmpIndex != -1)
                {
                    CmdStr = StringSDP.Substring(0, TmpIndex).Trim();
                    CmdValue = StringSDP.Substring(TmpIndex + 1).Trim();

                    if (CmdStr.ToUpper() == "i".ToUpper())
                    {
                        RetValue.SessionInfo = CmdValue.Trim();
                    }
                    else if (CmdStr.ToUpper() == "s".ToUpper())
                    {
                        RetValue.SessionName = CmdValue.Trim();
                    }
                    else if (CmdStr.ToUpper() == "m".ToUpper())
                    {
                        string[] CmdValueArray = null;
                        string RTPMapCode;

                        CmdValueArray = CmdValue.Split(" ");
                        RTPMapCode = CmdValueArray[CmdValueArray.Length - 1];

                        if (CmdValueArray[0].ToUpper() == "video".ToUpper())
                        {
                            if (RetValue.Video == null)
                            {
                                MediaType = 0;

                                MI = new SDPContent.SDPMapInfo();
                                MI.MediaType = CmdValueArray[0];
                                MI.RTPMap = CmdValueArray[CmdValueArray.Length - 1];

                                RetValue.Video = MI;

                                if (RetValue.MapList.ContainsKey(MI.RTPMap) == false)
                                    RetValue.MapList.Add(MI.RTPMap, MI);
                            }
                        }
                        else if (CmdValueArray[0].ToUpper() == "audio".ToUpper())
                        {
                            if (RetValue.Audio == null)
                            {
                                MediaType = 1;

                                MI = new SDPContent.SDPMapInfo();
                                MI.MediaType = CmdValueArray[0];
                                MI.RTPMap = CmdValueArray[CmdValueArray.Length - 1];

                                RetValue.Audio = MI;

                                if (RetValue.MapList.ContainsKey(MI.RTPMap) == false)
                                    RetValue.MapList.Add(MI.RTPMap, MI);
                            }
                        }
                        else
                        {
                            MediaType = 2;

                            MI = new SDPContent.SDPMapInfo();
                            MI.MediaType = CmdValueArray[0];
                            MI.RTPMap = CmdValueArray[CmdValueArray.Length - 1];

                            if (RetValue.MapList.ContainsKey(MI.RTPMap) == false)
                                RetValue.MapList.Add(MI.RTPMap, MI);
                        }
                    }
                    else if (CmdStr.ToUpper() == "a".ToUpper())
                    {
                        if (MediaType != -1)
                        {
                            string aControlStr;
                            string aControlValue = null;
                            int aControlIndex;

                            aControlIndex = CmdValue.IndexOf(":");
                            if (aControlIndex != -1)
                            {
                                aControlStr = CmdValue.Substring(0, aControlIndex).Trim();
                                aControlValue = CmdValue.Substring(aControlIndex + 1).Trim();
                            }
                            else
                            {
                                aControlStr = CmdValue.Trim();
                            }

                            if (aControlStr.ToUpper() == "fmtp".ToUpper())
                            {
                                // 找尋第一個空白
                                int TmpInt;

                                TmpInt = aControlValue.IndexOf(" ");
                                if (TmpInt != -1)
                                {
                                    string[] ValueArr = null;

                                    ValueArr = aControlValue.Substring(TmpInt + 1).Trim().Split(";");

                                    foreach (string EachValue in ValueArr)
                                    {
                                        string StringValue;

                                        StringValue = EachValue.Trim();
                                        TmpInt = StringValue.IndexOf("=");
                                        if (TmpInt != -1)
                                        {
                                            string vCmd;
                                            string vValue;

                                            vCmd = StringValue.Substring(0, TmpInt).Trim();
                                            vValue = StringValue.Substring(TmpInt + 1).Trim();

                                            if (MediaType == 0)
                                            {
                                                if (vCmd.ToUpper() == "sprop-parameter-sets".ToUpper())
                                                    MI.CodecConfig = vValue;
                                                else if (vCmd.ToUpper() == "config".ToUpper())
                                                    // MPEG4
                                                    // config=000001b001000001b58913000001000000012000c48d8800f514043c1463
                                                    MI.CodecConfig = vValue;
                                            }

                                            MI.FMTP.Set(vCmd, vValue);
                                        }
                                    }
                                }
                            }
                            else if (aControlStr.ToUpper() == "rtpmap".ToUpper())
                            {
                                string[] CodecInfoArr = null;
                                string CodecInfo;

                                CodecInfoArr = aControlValue.Split(" ");
                                if (CodecInfoArr.Length > 1)
                                {
                                    string ClockRate = string.Empty;
                                    string Channels = string.Empty;
                                    int TmpInt;

                                    CodecInfo = CodecInfoArr[1].Trim();
                                    TmpInt = CodecInfo.IndexOf("/");
                                    if (TmpInt != -1)
                                    {
                                        ClockRate = CodecInfo.Substring(TmpInt + 1).Trim();
                                        CodecInfo = CodecInfo.Substring(0, TmpInt).Trim();

                                        TmpInt = ClockRate.IndexOf("/");
                                        if (TmpInt != -1)
                                        {
                                            Channels = ClockRate.Substring(TmpInt + 1).Trim();
                                            ClockRate = ClockRate.Substring(0, TmpInt).Trim();
                                        }
                                    }

                                    MI.CodecName = CodecInfo;
                                    MI.Freq = ClockRate;
                                    MI.Channels = Channels;
                                }
                            }
                            else if (aControlStr.ToUpper() == "control".ToUpper())
                            {
                                MI.Control = aControlValue;
                            }
                        }
                    }
                }
            }

            return RetValue;
        }

        public partial class SDPContent
        {
            public Uri LocationBase;
            public string SessionName;
            public string SessionInfo;
            public SDPMapInfo Video = null;
            public SDPMapInfo Audio = null;
            public Dictionary<string, SDPMapInfo> MapList = new Dictionary<string, SDPMapInfo>();

            public partial class SDPMapInfo
            {
                public string MediaType;
                public string Control;
                public string RTPMap;
                public string Freq;
                public string Channels;
                public string CodecName;
                public string CodecConfig;
                public RTSPHeaderSet FMTP = new RTSPHeaderSet();
            }
        }
    }
}