using System;
using UnityEngine;

namespace ReincarnationLog.Runtime
{
    public interface IAdReviveService
    {
        void ShowReviveAd(Action<bool> onClosed);
    }

    public class DebugAdReviveService : IAdReviveService
    {
        public void ShowReviveAd(Action<bool> onClosed)
        {
            Debug.Log("[Ad] Simulated interstitial ad watched.");
            onClosed?.Invoke(true);
        }
    }
}
