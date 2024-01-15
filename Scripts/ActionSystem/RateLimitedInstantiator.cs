using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace mtion.room.sdk.action
{
    public abstract class RateLimitedInstantiator : MonoBehaviour
    {
        protected abstract int ObjectLimit { get; }

        private int numObjects;
        private Queue<Action> spawnQueue = new Queue<Action>();
        private Queue<Action> deleteQueue = new Queue<Action>();

        protected void EnqueueInstantiation(Action action)
        {
            if (numObjects < ObjectLimit)
            {
                action?.Invoke();
                numObjects++;
            }
            else
            {
                spawnQueue.Enqueue(action);
            }
        }

        protected void EnqueueInstantiationCleanup(Action action)
        {
            deleteQueue.Enqueue(action);
        }

        protected void DeleteOldestObjects(int amount)
        {
            int deleted = 0;
            while (deleteQueue.Count > 0 &&
                deleted < amount)
            {
                var toDelete = deleteQueue.Dequeue();
                toDelete?.Invoke();
                deleted++;
                numObjects--;
            }
        }

        protected void Update()
        {
            if (spawnQueue.Count == 0)
            {
                return;
            }

            if (numObjects >= ObjectLimit &&
                deleteQueue.Count > 0)
            {
                var amtToDelete = Mathf.Min(spawnQueue.Count, ObjectLimit) / 100f;
                DeleteOldestObjects(Mathf.CeilToInt(amtToDelete));
            }

            while (numObjects < ObjectLimit &&
                spawnQueue.Count > 0)
            {
                var spawnAction = spawnQueue.Dequeue();
                spawnAction?.Invoke();
                numObjects++;
            }
        }
    }

}
