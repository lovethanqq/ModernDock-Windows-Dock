using System;
using System.Threading;

namespace MyCustomDock
{
    public sealed class SingleInstanceGuard : IDisposable
    {
        private readonly Mutex mutex;
        private readonly bool ownsMutex;
        private bool disposed;

        private SingleInstanceGuard(Mutex mutex, bool ownsMutex)
        {
            this.mutex = mutex;
            this.ownsMutex = ownsMutex;
        }

        public static SingleInstanceGuard TryAcquire(string name)
        {
            Mutex candidate = null;
            bool createdNew = false;
            bool owns = false;
            try
            {
                candidate = new Mutex(true, name, out createdNew);
                owns = createdNew;
            }
            catch (AbandonedMutexException)
            {
                // The abandoned mutex is acquired by this constructor call.
                owns = true;
            }

            if (!owns)
            {
                if (candidate != null) candidate.Dispose();
                return null;
            }

            return new SingleInstanceGuard(candidate, true);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            if (ownsMutex)
            {
                try { mutex.ReleaseMutex(); } catch (ApplicationException) { }
            }
            mutex.Dispose();
        }
    }
}
