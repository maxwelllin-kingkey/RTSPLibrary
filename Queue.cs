using System.Collections.Generic;

namespace RTSPLibrary {
    internal partial class Queue<T>
    {
        private List<T> iList = new List<T>();
        private int iQueueCount = 0;

        public virtual IEnumerable<T> Items
        {
            get { return iList; }
        }

        public virtual T[] ToArray()
        {
            return iList.ToArray();
        }

        public virtual int Count
        {
            get { return iList.Count; }
        }

        public virtual void EnqueueRange(T[] obj)
        {
            lock (iList)
            {
                foreach (T EachT in obj)
                    Enqueue(EachT);
            }
        }

        public virtual void Enqueue(T obj)
        {
            lock (iList)
            {
                iList.Add(obj);

                if (iQueueCount > 0)
                {
                    if (iList.Count > iQueueCount)
                        iList.RemoveAt(0);
                }
            }
        }

        public virtual T Dequeue()
        {
            T RetValue = default(T);

            lock (iList)
            {
                if (iList.Count > 0)
                {
                    RetValue = iList[0];
                    iList.RemoveAt(0);
                }
            }

            return RetValue;
        }

        public virtual T PeekFirst()
        {
            T RetValue = default(T);

            lock (iList)
            {
                if (iList.Count > 0)
                    RetValue = iList[iList.Count - 1];
            }

            return RetValue;
        }

        public virtual T PeekLast()
        {
            T RetValue = default(T);

            lock (iList)
            {
                if (iList.Count > 0)
                    RetValue = iList[0];
            }

            return RetValue;
        }

        public virtual T PeekIndex(int index)
        {
            return iList[index];
        }

        public bool FindExist(T o)
        {
            bool Found = false;

            lock (iList)
            {
                foreach (T EachObj in iList)
                {
                    if (EachObj.Equals(o))
                    {
                        Found = true;
                        break;
                    }
                }
            }

            return Found;
        }

        public virtual void Clear()
        {
            lock (iList)
            {
                iList.Clear();
            }
        }

        public Queue()
        {
            iQueueCount = 0;
        }

        public Queue(int MaxQueueCount)
        {
            iQueueCount = MaxQueueCount;
        }
    }
}
