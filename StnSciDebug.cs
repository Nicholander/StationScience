using System;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace StationScience
{
    public static class StnSciDebug
    {
        [Conditional("DEBUG")]
        public static void DebugLog(string message)
            => Debug.Log("[StnSci] " + message);

        public static void Log(string message)
            => Debug.Log("[StnSci] " + message);
        public static void LogError(string error)
            => Debug.LogError("[StnSci] " + error);
        public static void LogWarning(string warning)
            => Debug.LogWarning("[StnSci] " + warning);
        public static void LogException(Exception ex)
            => Debug.LogException(ex);
    }
}
