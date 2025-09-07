using System.Collections.Generic;
using UnityEngine;

namespace CyberSpeed.CardsMatchGame
{
    public class ObjectPool : MonoBehaviour, IObjectPool
    {
        public bool mWillGrowPool = true;

        //Prefab to Object pool
        private GameObject mPrefab;
        //Is it able to Grow the Pool

        //Define Pool Size
        private int mPoolSize = 4;
        private Transform mParentTransform;
        private List<GameObject> mPooledObjects;
        private GameObject mObject;

        //Instantiate the Prefabs and assign to the List for Pooling
        public void InitializePool(GameObject inPrefab, int inPoolSize = 4)
        {
            mPoolSize = inPoolSize;
            mPrefab = inPrefab;
            //if (mPoolSize == 0)
            //mPoolSize = MGConstants.PoolCount;

            mParentTransform = this.transform;

            mPooledObjects = new List<GameObject>();
            for (int index = 0; index < mPoolSize; index++)
            {
                mObject = GameObject.Instantiate(mPrefab, mParentTransform) as GameObject;
                mPooledObjects.Add(mObject);
                mObject.SetActive(false);
            }

            mObject = null;
        }

        ///  <summary>To Get the curretly Diactivated Object in the Hierarchy  </summary>
        public GameObject GetObjectFromPool()
        {
            for (int index = 0; index < mPooledObjects.Count; index++)
            {
                if (!mPooledObjects[index].activeInHierarchy)
                    return mPooledObjects[index];
            }

            if (mWillGrowPool)
            {
                mObject = GameObject.Instantiate(mPrefab, mParentTransform) as GameObject;
                mPooledObjects.Add(mObject);
                return mObject;
            }

            return null;
        }

        /// <summary>Deactivate all objects in the pool</summary>
        public void ReleaseAll()
        {
            if (mPooledObjects == null) return;
			
            for (int index = 0; index < mPooledObjects.Count; index++)
            {
                if (mPooledObjects[index] != null)
                {
                    mPooledObjects[index].SetActive(false);
                }
            }
        }
    }
}