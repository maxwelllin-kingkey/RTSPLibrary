using System;
using System.Text;

namespace RTSPLibrary
{
    public partial class RTSPAuthorization
    {
        protected string iAccount = string.Empty;
        protected string iPassword = string.Empty;
        private string iWWWAuthHeader;
        private enumWWWAuthorizeType iWWWAuthType = enumWWWAuthorizeType.BASIC;
        private long iCnonce = System.DateTime.Now.Ticks;
        private long nc = 0L;

        public enum enumWWWAuthorizeType
        {
            BASIC = 0,
            DIGEST = 1
        }

        public enumWWWAuthorizeType AuthType
        {
            get
            {
                return iWWWAuthType;
            }
        }

        public string ToHeaderString(string URL, string Method)
        {
            switch (iWWWAuthType)
            {
                case enumWWWAuthorizeType.BASIC:
                    return "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(iAccount + ":" + iPassword));
                case enumWWWAuthorizeType.DIGEST:
                    string Realm = null;
                    string Nonce = null;
                    string Qop = null;
                    string QopAuthStr = null;
                    string Opaque = null;
                    bool isQopAuth = false;
                    string cnonceStr;
                    string ncStr;
                    string AuthType = null;

                    SplitAuthHeader(iWWWAuthHeader, ref AuthType, ref Realm, ref Nonce, ref Qop, ref Opaque);
                    if (string.IsNullOrEmpty(Qop) == false)
                    {
                        string[] QopAuthArray = null;
                        bool HasAuthInt = false;
                        bool HasAuth = false;

                        QopAuthArray = Qop.Split(",");
                        foreach (string EachStr in QopAuthArray)
                        {
                            if (EachStr.Trim().ToUpper() == "AUTH")
                                HasAuth = true;
                            else if (EachStr.Trim().ToUpper() == "AUTH-INT")
                                HasAuthInt = true;
                        }

                        if (HasAuth)
                        {
                            // 優先使用 "Auth", 因為不需要編碼 Body 內容
                            QopAuthStr = "auth";
                            isQopAuth = true;
                        }
                        else if (HasAuthInt)
                        {
                            QopAuthStr = "auth-int";
                            isQopAuth = true;
                        }
                    }

                    if (isQopAuth)
                    {
                        cnonceStr = GetHash(BitConverter.GetBytes(iCnonce));

                        ncStr = nc.ToString("X");
                        if (ncStr.Length < 8)
                            ncStr = new string('0', 8 - ncStr.Length) + ncStr;

                        nc++;
                        if (string.IsNullOrEmpty(Opaque))
                            return "Digest username=\"" + iAccount + "\", realm=\"" + Realm + "\", nonce=\"" + Nonce + "\", uri=\"" + URL + "\", response=\"" + CalcDigestAuthCode(iAccount, iPassword, Nonce, cnonceStr, ncStr, QopAuthStr, Realm, Method, URL, null) + "\", qop=\"" + QopAuthStr + "\", nc=" + ncStr + ", cnonce=\"" + cnonceStr + "\"";
                        else
                            return "Digest username=\"" + iAccount + "\", realm=\"" + Realm + "\", nonce=\"" + Nonce + "\", uri=\"" + URL + "\", response=\"" + CalcDigestAuthCode(iAccount, iPassword, Nonce, cnonceStr, ncStr, QopAuthStr, Realm, Method, URL, null) + "\", opaque=\"" + Opaque + "\", qop=\"" + QopAuthStr + "\", nc=" + ncStr + ", cnonce=\"" + cnonceStr + "\"";
                    }
                    else
                        return "Digest username=\"" + iAccount + "\", realm=\"" + Realm + "\", nonce=\"" + Nonce + "\", uri=\"" + URL + "\", response=\"" + CalcDigestAuthCode(iAccount, iPassword, Nonce, Realm, Method, URL) + "\"";
                default:
                    return null;
            }
        }

        public RTSPAuthorization(RTSPAuthorization RTSPAuth, string ResponseWWWAuthHeader)
        {
            string AuthType = null;
            string Realm = null;
            string Nonce = null;
            string Qop = null;
            string Opaque = null;

            iAccount = RTSPAuth.iAccount;
            iPassword = RTSPAuth.iPassword;
            iWWWAuthHeader = ResponseWWWAuthHeader;

            SplitAuthHeader(iWWWAuthHeader, ref AuthType, ref Realm, ref Nonce, ref Qop, ref Opaque);

            if (AuthType.ToUpper() == "BASIC".ToUpper())
                iWWWAuthType = enumWWWAuthorizeType.BASIC;
            else if (AuthType.ToUpper() == "DIGEST".ToUpper())
                iWWWAuthType = enumWWWAuthorizeType.DIGEST;
        }

        public RTSPAuthorization(string Account, string Password)
        {
            iAccount = Account;
            iPassword = Password;
            iWWWAuthType = enumWWWAuthorizeType.BASIC;
        }

        public RTSPAuthorization(string Account, string Password, string ResponseWWWAuthHeader)
        {
            string AuthType = null;
            string Realm = null;
            string Nonce = null;
            string Qop = null;
            string Opaque = null;

            iAccount = Account;
            iPassword = Password;

            SplitAuthHeader(iWWWAuthHeader, ref AuthType, ref Realm, ref Nonce, ref Qop, ref Opaque);

            if (AuthType.ToUpper() == "BASIC".ToUpper())
                iWWWAuthType = enumWWWAuthorizeType.BASIC;
            else if (AuthType.ToUpper() == "DIGEST".ToUpper())
                iWWWAuthType = enumWWWAuthorizeType.DIGEST;
        }

        private void SplitAuthHeader(string S, ref string outAuthType, ref string outRealm, ref string outNonce, ref string outQop, ref string outOpaque)
        {
            int FirstInt;
            int LastInt;
            string TmpStr;
            string AuthType;
            RTSPHeaderSet iNameList = default;
            int TmpInt;
            string NameHead;
            string NameValue;
            bool RetValue = false;

            FirstInt = S.IndexOf(" ");
            if (FirstInt != -1)
            {
                AuthType = S.Substring(0, FirstInt);
                TmpStr = S.Substring(FirstInt + 1);
            }
            else
            {
                AuthType = S;
                TmpStr = string.Empty;
            }

            iNameList = new RTSPHeaderSet();

            while (true)
            {
                TmpInt = TmpStr.IndexOf("=");
                if (TmpInt != -1)
                {
                    NameHead = TmpStr.Substring(0, TmpInt).Trim();
                    TmpStr = TmpStr.Substring(TmpInt + 1).Trim();

                    // find first "
                    FirstInt = TmpStr.IndexOf("\"");
                    if (FirstInt != -1)
                    {
                        // Find next "
                        LastInt = TmpStr.IndexOf("\"", FirstInt + 1);

                        NameValue = TmpStr.Substring(FirstInt + 1, LastInt - FirstInt - 1);
                        TmpStr = TmpStr.Substring(LastInt + 1).Trim();

                        iNameList.Set(NameHead, NameValue);
                    }
                    else
                    {
                        // not found
                        // find ,
                        TmpInt = TmpStr.IndexOf(",");
                        if (TmpInt != -1)
                        {
                            // have next
                            NameValue = TmpStr.Substring(0, TmpInt).Trim();
                            TmpStr = TmpStr.Substring(TmpInt).Trim();

                            iNameList.Set(NameHead, NameValue);
                        }
                        else
                        {
                            // last value
                            NameValue = TmpStr.Trim();
                            iNameList.Set(NameHead, NameValue);

                            break;
                        }
                    }

                    TmpInt = TmpStr.IndexOf(",");
                    if (TmpInt != -1)
                        TmpStr = TmpStr.Substring(TmpInt + 1);
                }
                else
                    // decode completed
                    break;
            }

            if (iNameList != null)
            {
                RetValue = true;
                outNonce = iNameList["nonce"];
                outRealm = iNameList["realm"];
                outAuthType = AuthType;
                outQop = iNameList["qop"];
                outOpaque = iNameList["opaque"];
            }
        }

        private string CalcDigestAuthCode(string Username, string Password, string AuthorizationHeader, string Method, string URL, byte[] SendBody)
        {
            // WWW-Authenticate: Digest realm="LIVE555 Streaming Media", nonce="b0235d6f4e314599ef504f3891dd5f0a"
            var Nonce = default(string);
            var Realm = default(string);
            var AuthType = default(string);
            var Qop = default(string);
            var Opaque = default(string);
            bool IsQopAuth = false;

            SplitAuthHeader(AuthorizationHeader, ref AuthType, ref Realm, ref Nonce, ref Qop, ref Opaque);
            if (AuthType.ToUpper() == "DIGEST")
                return CalcDigestAuthCode(Username, Password, Nonce, Realm, Method, URL);
            else
                return null;
        }

        private string CalcDigestAuthCode(string Username, string Password, string Nonce, string Realm, string Method, string URL)
        {
            string Hash1;
            string Hash2;
            string Hash3;
            // 0090   // The "response" field is computed as:
            // 00091   //    md5(md5(<username>:<realm>:<password>):<nonce>:md5(<cmd>:<url>))
            // 00092   // or, if "fPasswordIsMD5" is True:
            // 00093   //    md5(<password>:<nonce>:md5(<cmd>:<url>))

            Hash1 = GetHash(Username + ":" + Realm + ":" + Password);
            Hash2 = GetHash(Method + ":" + URL);
            Hash3 = GetHash(Hash1 + ":" + Nonce + ":" + Hash2);

            return Hash3;
        }

        private string CalcDigestAuthCode(string Username, string Password, string Nonce, string CNonce, string nc, string qop, string Realm, string Method, string URL, byte[] SendBody)
        {
            string Hash1;
            var Hash2 = default(string);
            string Hash3;
            // 0090   // The "response" field is computed as:
            // 00091   //    md5(md5(<username>:<realm>:<password>):<nonce>:md5(<cmd>:<url>))
            // 00092   // or, if "fPasswordIsMD5" is True:
            // 00093   //    md5(<password>:<nonce>:md5(<cmd>:<url>))

            Hash1 = GetHash(Username + ":" + Realm + ":" + Password);
            if (string.IsNullOrEmpty(qop) == false)
            {
                if (qop.ToUpper() == "Auth".ToUpper())
                {
                    Hash2 = GetHash(Method + ":" + URL);
                }
                else if (qop.ToUpper() == "Auth-Int".ToUpper())
                {
                    // (A2 = md5(request-method:uri:md5(request-body)) 
                    string HashBody;

                    if (SendBody != null)
                        HashBody = GetHash(SendBody);
                    else
                        HashBody = string.Empty;

                    Hash2 = GetHash(Method + ":" + URL + ":" + GetHash(HashBody));
                }

                Hash3 = GetHash(Hash1 + ":" + Nonce + ":" + nc + ":" + CNonce + ":" + qop + ":" + Hash2);
            }
            else
            {
                Hash2 = GetHash(Method + ":" + URL);
                Hash3 = GetHash(Hash1 + ":" + Nonce + ":" + Hash2);
            }

            return Hash3;
        }

        private string GetHash(string strIn)
        {
            return GetHash(Encoding.UTF8.GetBytes(strIn));
        }

        private string GetHash(byte[] DataIn)
        {
            var MD5 = new System.Security.Cryptography.MD5CryptoServiceProvider();
            var bytMD5Ptr = MD5.ComputeHash(DataIn);
            string strHash = BitConverter.ToString(bytMD5Ptr);

            strHash = strHash.Replace("-", "").ToLower();

            return strHash;
        }
    }
}