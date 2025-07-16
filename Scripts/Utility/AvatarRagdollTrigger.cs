using UnityEngine;

namespace mtion.room
{
    public sealed class AvatarRagdollTrigger : MonoBehaviour
    {
        [SerializeField] private int _damage = 1;

        public int Damage => _damage;
    }
}
