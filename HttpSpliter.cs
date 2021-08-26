using System.Collections.Generic;
using System.Linq;

namespace RTSPLibrary
{
    internal partial class HttpSpliter
    {
        public int FindNextLineBreak(IEnumerable<byte> Data, int startOffset, ref int NextDataStart)
        {
            int RetValue = -1;
            int DataCount = Data.Count();
            bool Found = false;

            for (int I = startOffset; I<DataCount; I++)
            {
                if (Data.ElementAtOrDefault(I) == 13)
                {
                    // 先找到 13
                    RetValue = I;
                    NextDataStart = I + 1;

                    if ((DataCount - 1) >= (I + 1))
                    {
                        if (Data.ElementAtOrDefault(I + 1) == 10)
                            NextDataStart = I + 2;
                    }

                    break;
                }
                else if (Data.ElementAtOrDefault(I) == 10)
                {
                    // 先找到 10
                    RetValue = I;
                    NextDataStart = I + 1;

                    if ((DataCount - 1) >= (I + 1))
                    {
                        if (Data.ElementAtOrDefault(I + 1) == 13)
                            NextDataStart = I + 2;
                    }

                    break;
                }
            }

            return RetValue;
        }

        public int FindNextLineBreak(UltimateByteArrayClass HBA, int startOffset, ref int NextDataStart)
        {
            int RetValue = -1;
            bool Found = false;
            byte[] Data = null;

            Data = HBA.InternalBuffer;
            for (int I = startOffset; I < HBA.Count; I++)
            {
                if (Data[I] == 13)
                {
                    // 先找到 13
                    RetValue = I;
                    NextDataStart = I + 1;

                    if ((Data.Length - 1) >= (I + 1))
                    {
                        if (Data[I + 1] == 10)
                            NextDataStart = I + 2;
                    }

                    break;
                }
                else if (Data[I] == 10)
                {
                    // 先找到 10
                    RetValue = I;
                    NextDataStart = I + 1;

                    if ((Data.Length - 1) >= (I + 1))
                    {
                        if (Data[I + 1] == 13)
                            NextDataStart = I + 2;
                    }

                    break;
                }
            }

            return RetValue;
        }

        public string[] SplitContent(string Data)
        {
            string TmpStr;
            var iList = new List<string>();
            string[] RetValue = null;

            TmpStr = Data;

            if (string.IsNullOrEmpty(TmpStr) == false)
            {
                while (true)
                {
                    bool Found = false;

                    if (string.IsNullOrEmpty(TmpStr))
                        break;

                    for (int I = 0; I < TmpStr.Length; I++)
                    {
                        if (TmpStr[I] == '\r')
                        {
                            iList.Add(TmpStr.Substring(0, I));

                            if ((TmpStr.Length - 1) >= (I + 1))
                            {
                                if (TmpStr[I + 1] == '\n')
                                    TmpStr = TmpStr.Substring(I + 2);
                                else
                                    TmpStr = TmpStr.Substring(I + 1);
                            }
                            else
                            {
                                TmpStr = TmpStr.Substring(I + 1);
                            }

                            Found = true;

                            break;
                        }
                        else if (TmpStr[I] == '\n')
                        {
                            iList.Add(TmpStr.Substring(0, I));

                            if ((TmpStr.Length - 1) >= (I + 1))
                            {
                                if (TmpStr[I + 1] == '\r')
                                    TmpStr = TmpStr.Substring(I + 2);
                                else
                                    TmpStr = TmpStr.Substring(I + 1);
                            }
                            else
                            {
                                TmpStr = TmpStr.Substring(I + 1);
                            }

                            Found = true;

                            break;
                        }
                    }

                    if (Found == false)
                    {
                        iList.Add(TmpStr);
                        TmpStr = string.Empty;
                    }
                }
            }

            if (iList.Count > 0)
            {
                RetValue = iList.ToArray();
                iList.Clear();
            }
            else
            {
                RetValue = new string[] { Data };
            }

            return RetValue;
        }

        public int FindDoubleLineBreak(IEnumerable<byte> Data, ref int BodyIndex)
        {
            int RetValue = -1;
            int FindIndex = -1;
            int NextIndex = 0;
            int DataCount = Data.Count();
            bool Found = false;
            bool HasFullCrLf = false;

            for (int I = 0; I < DataCount; I++)
            {
                if (Data.ElementAtOrDefault(I) == 13)
                {
                    // 先找到 13
                    if (FindIndex == -1)
                    {
                        FindIndex = I;

                        if ((DataCount - 1) >= (I + 1))
                        {
                            if (Data.ElementAtOrDefault(I + 1) == 10)
                            {
                                I += 1;
                                HasFullCrLf = true;
                            }
                        }
                    }
                    else
                    {
                        // 已找到兩層 LineBreak
                        NextIndex = I + 1;

                        if (HasFullCrLf)
                        {
                            if ((DataCount - 1) >= (I + 1))
                            {
                                if (Data.ElementAtOrDefault(I + 1) == 10)
                                    NextIndex += 1;

                                Found = true;
                            }
                        }
                        else
                        {
                            Found = true;
                        }

                        break;
                    }
                }
                else if (Data.ElementAtOrDefault(I) == 10)
                {
                    // 先找到 10
                    if (FindIndex == -1)
                    {
                        FindIndex = I;

                        if ((DataCount - 1) >= (I + 1))
                        {
                            if (Data.ElementAtOrDefault(I + 1) == 13)
                            {
                                I += 1;
                                HasFullCrLf = true;
                            }
                        }
                    }
                    else
                    {
                        NextIndex = I + 1;

                        if (HasFullCrLf)
                        {
                            if ((DataCount - 1) >= (I + 1))
                            {
                                if (Data.ElementAtOrDefault(I + 1) == 13)
                                    NextIndex += 1;

                                Found = true;
                            }
                        }
                        else
                        {
                            Found = true;
                        }

                        break;
                    }
                }
                else
                {
                    FindIndex = -1;
                }
            }

            if (Found)
            {
                RetValue = FindIndex;
                BodyIndex = NextIndex;
            }
            else
            {
                RetValue = -1;
            }

            return RetValue;
        }

        public int FindDoubleLineBreak(UltimateByteArrayClass HBA, ref int BodyIndex)
        {
            int RetValue = -1;
            int FindIndex = -1;
            int NextIndex = 0;
            bool Found = false;
            bool HasFullCrLf = false;
            byte[] Data = null;

            Data = HBA.InternalBuffer;

            for (int I = 0; I < HBA.Count; I++)
            {
                if (Data[I] == 13)
                {
                    // 先找到 13
                    if (FindIndex == -1)
                    {
                        FindIndex = I;

                        if ((Data.Length - 1) >= (I + 1))
                        {
                            if (Data[I + 1] == 10)
                            {
                                I += 1;
                                HasFullCrLf = true;
                            }
                        }
                    }
                    else
                    {
                        // 已找到兩層 LineBreak
                        NextIndex = I + 1;

                        if (HasFullCrLf)
                        {
                            if ((Data.Length - 1 )>= (I + 1))
                            {
                                if (Data[I + 1] == 10)
                                    NextIndex += 1;

                                Found = true;
                            }
                        }
                        else
                        {
                            Found = true;
                        }

                        break;
                    }
                }
                else if (Data[I] == 10)
                {
                    // 先找到 10
                    if (FindIndex == -1)
                    {
                        FindIndex = I;

                        if ((Data.Length - 1) >= (I + 1))
                        {
                            if (Data[I + 1] == 13)
                            {
                                I += 1;
                                HasFullCrLf = true;
                            }
                        }
                    }
                    else
                    {
                        NextIndex = I + 1;

                        if (HasFullCrLf)
                        {
                            if ((Data.Length - 1) >= (I + 1))
                            {
                                if (Data[I + 1] == 13)
                                    NextIndex += 1;

                                Found = true;
                            }
                        }
                        else
                        {
                            Found = true;
                        }

                        break;
                    }
                }
                else
                {
                    FindIndex = -1;
                }
            }

            if (Found)
            {
                RetValue = FindIndex;
                BodyIndex = NextIndex;
            }
            else
            {
                RetValue = -1;
            }

            return RetValue;
        }
    }
}
