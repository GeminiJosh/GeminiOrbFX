using System.Collections.Generic;

namespace GeminiOrbFX.UI.Services
{
    internal static class OrbQueueService
    {
        private static readonly object _spawnQueueLock = new object();
        private static readonly Queue<OrbRequest> _spawnQueue = new Queue<OrbRequest>();

        internal static int GetQueueCount()
        {
            lock (_spawnQueueLock)
            {
                return _spawnQueue.Count;
            }
        }

        internal static void ClearQueue()
        {
            lock (_spawnQueueLock)
            {
                _spawnQueue.Clear();
            }
        }

        internal static object QueueLock => _spawnQueueLock;
        internal static Queue<OrbRequest> Queue => _spawnQueue;
    }
}