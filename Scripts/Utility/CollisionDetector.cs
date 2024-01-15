using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace mtion.room
{
    public class CollisionDetector : MonoBehaviour
    {
        private Rigidbody _rigidbody;

        public delegate void OnCollisionDelegate(Rigidbody detectorRb, Collision collision);
        public delegate void OnExplosionDelegate(Rigidbody detectorRb, float explosionForce, Vector3 explosionPos);

        public OnCollisionDelegate OnCollision;

        public OnExplosionDelegate OnExplosion;

        public void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
        }

        public void OnCollisionEnter(Collision collision)
        {

            OnCollision?.Invoke(_rigidbody, collision);
        }

        public void AddExplosionForce(float explosionForce, Vector3 explosionPos)
        {
            OnExplosion?.Invoke(_rigidbody, explosionForce, explosionPos);
        }
    }
}
