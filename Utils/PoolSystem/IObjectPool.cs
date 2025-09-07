using UnityEngine;

namespace CyberSpeed.CardsMatchGame
{
    public interface IObjectPool
    {
        void InitializePool(GameObject inPrefab, int inPoolSize = 4);
        GameObject GetObjectFromPool();
        void ReleaseAll();
    }
}


