using System.Collections.Generic;
using System.Threading;

namespace DataExtractor
{
    public class ProducerConsumerQueue<T>
    {
        object _queueLock = new();
        Queue<T> _queue = new();
        volatile bool _shutdown;

        public ProducerConsumerQueue()
        {
            _shutdown = false;
        }

        public void Push(T value)
        {
            lock (_queueLock)
            {
                _queue.Enqueue(value);
                Monitor.PulseAll(_queueLock);
            }
        }

        public bool Empty()
        {
            lock (_queueLock)
                return _queue.Count == 0;
        }

        public bool Pop(out T value)
        {
            value = default(T);
            lock (_queueLock)
            {
                if (_queue.Count == 0 || _shutdown)
                    return false;

                value = _queue.Dequeue();
                return true;
            }
        }

        public void WaitAndPop(out T value)
        {
            value = default(T);
            lock (_queueLock)
            {
                while (_queue.Count == 0 && !_shutdown)
                    Monitor.Wait(_queueLock);

                if (_queue.Count == 0 || _shutdown)
                    return;

                value = _queue.Dequeue();
            }
        }

        public void Cancel()
        {
            lock (_queueLock)
            {
                while (_queue.Count != 0)
                {
                    _queue.Dequeue();
                }

                _shutdown = true;
                Monitor.PulseAll(_queueLock);
            }
        }
    }
}
