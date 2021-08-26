using System;
using System.Collections.Generic;
using System.Linq;

namespace RTSPLibrary
{
    public partial class RTSPHeaderSet
    {
        private HeaderClass iHeader = new HeaderClass();

        public string this[string Name]
        {
            get { return iHeader.GetValue(Name); }
            set { iHeader.Set(Name, value); }
        }

        public string[] GetValues(string Name)
        {
            return iHeader.GetValues(Name);
        }

        public bool HasKey(string Name)
        {
            return iHeader.HasKey(Name);
        }

        public int Count
        {
            get { return iHeader.Count; }
        }

        public string[] GetKeys()
        {
            return iHeader.GetKeys();
        }

        public void Set(string Name, string Value)
        {
            iHeader.Set(Name, Value);
        }

        public void Clear()
        {
            iHeader.Clear();
        }

        [Serializable()]
        public partial class HeaderClass
        {
            private Dictionary<string, List<HeaderItemClass>> HeaderInternalValue = new Dictionary<string, List<HeaderItemClass>>();

            public bool HasKey(string name)
            {
                return HeaderInternalValue.ContainsKey(name.ToUpper());
            }

            public int Count
            {
                get { return HeaderInternalValue.Count; }
            }

            public string[] GetKeys()
            {
                List<string> RetValue = new List<string>();

                lock (HeaderInternalValue)
                {
                    List<HeaderItemClass>[] HHIList = null;

                    HHIList = HeaderInternalValue.Values.ToArray();
                    foreach (List<HeaderItemClass> EachHHI in HHIList)
                    {
                        if (EachHHI != null)
                        {
                            if (EachHHI.Count > -0)
                                RetValue.Add(EachHHI[0].Name);
                        }
                    }
                }

                return RetValue.ToArray();
            }

            public void Clear()
            {
                lock (HeaderInternalValue)
                {
                    HeaderInternalValue.Clear();
                }
            }

            public void Clear(string name)
            {
                lock (HeaderInternalValue)
                {
                    HeaderInternalValue.Remove(name.ToUpper());
                }
            }

            public void Add(string name, string value)
            {
                List<HeaderItemClass> NameList = null;

                NameList = GetNameList(name);

                lock (NameList)
                {
                    NameList.Add(new HeaderItemClass() { Name = name, Value = value });
                }
            }

            public void Set(string name, string value)
            {
                List<HeaderItemClass> NameList = null;

                NameList = GetNameList(name);

                lock (NameList)
                {
                    NameList.Clear();
                    NameList.Add(new HeaderItemClass() { Name = name, Value = value });
                }
            }

            public string GetValue(string name)
            {
                string RetValue = null;
                List<HeaderItemClass> NameList = null;

                NameList = GetNameList(name);
                if (NameList != null)
                {
                    if (NameList.Count > 0)
                    {
                        lock (NameList)
                        {
                            RetValue = NameList[NameList.Count - 1].Value;
                        }
                    }
                }

                return RetValue;
            }

            public string[] GetValues(string name)
            {
                List<string> RetValue = new List<string>();
                List<HeaderItemClass> NameList = null;

                NameList = GetNameList(name);
                if (NameList != null)
                {
                    lock (NameList)
                    {
                        foreach (HeaderItemClass EachHHI in NameList)
                            RetValue.Add(EachHHI.Value);
                    }
                }

                return RetValue.ToArray();
            }

            public string ToHeaderString()
            {
                string RetValue = string.Empty;

                lock (HeaderInternalValue)
                {
                    List<HeaderItemClass>[] HHIList = null;

                    HHIList = HeaderInternalValue.Values.ToArray();
                    foreach (List<HeaderItemClass> EachHHIList in HHIList)
                    {
                        foreach (HeaderItemClass EachHHI in EachHHIList)
                        {
                            RetValue += EachHHI.Name + ": " + EachHHI.Value + "\r\n";
                        }
                    }
                }

                return RetValue;
            }

            private List<HeaderItemClass> GetNameList(string name)
            {
                List<HeaderItemClass> RetValue = null;

                lock (HeaderInternalValue)
                {
                    if (HeaderInternalValue.ContainsKey(name.ToUpper()) == false)
                    {
                        RetValue = new List<HeaderItemClass>();
                        HeaderInternalValue.Add(name.ToUpper(), RetValue);
                    }
                    else
                    {
                        RetValue = HeaderInternalValue[name.ToUpper()];
                    }
                }

                return RetValue;
            }

            public partial class HeaderItemClass
            {
                public string Name;
                public string Value;
            }
        }
    }
}