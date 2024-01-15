using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace mtion.room
{
    public class AvatarRagdollTrigger : MonoBehaviour
    {
        [SerializeField] private int _damage = 1;

        public int Damage => _damage;
    }
}
