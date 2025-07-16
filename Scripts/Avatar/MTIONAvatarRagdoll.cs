using mtion.room.sdk.action;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace mtion.room.sdk
{
    public class MTIONAvatarRagdoll : MTIONComponent
    {
        [Header("Bones")]
        [SerializeField] private Transform _root;
        [SerializeField] private Transform _hips;
        [SerializeField] private Transform _spine;
        [SerializeField] private Transform _chest;
        [SerializeField] private Transform _head;
        [SerializeField] private Transform _leftUpperLeg;
        [SerializeField] private Transform _leftLowerLeg;
        [SerializeField] private Transform _leftFoot;
        [SerializeField] private Transform _rightUpperLeg;
        [SerializeField] private Transform _rightLowerLeg;
        [SerializeField] private Transform _rightFoot;
        [SerializeField] private Transform _leftUpperArm;
        [SerializeField] private Transform _leftLowerArm;
        [SerializeField] private Transform _leftHand;
        [SerializeField] private Transform _rightUpperArm;
        [SerializeField] private Transform _rightLowerArm;
        [SerializeField] private Transform _rightHand;

        [Header("Additional Parameters")]
        [SerializeField] private float _weight = 75f;

        [Header("Auto Populate")]
        [SerializeField] private Animator _animator;

        public Transform Root => _root;
        public Transform Hips => _hips;
        public Transform Spine => _spine;
        public Transform Chest => _chest;
        public Transform Head => _head;
        public Transform LeftUpperLeg => _leftUpperLeg;
        public Transform LeftLowerLeg => _leftLowerLeg;
        public Transform LeftFoot => _leftFoot;
        public Transform RightUpperLeg => _rightUpperLeg;
        public Transform RightLowerLeg => _rightLowerLeg;
        public Transform RightFoot => _rightFoot;
        public Transform LeftUpperArm => _leftUpperArm;
        public Transform LeftLowerArm => _leftLowerArm;
        public Transform LeftHand => _leftHand;
        public Transform RightUpperArm => _rightUpperArm;
        public Transform RightLowerArm => _rightLowerArm;
        public Transform RightHand => _rightHand;

        public float Weight => _weight;

        public bool HasRequiredBones()
        {
            if (_root == null ||
                _hips == null ||
                _head == null ||
                _leftUpperLeg == null ||
                _leftLowerLeg == null ||
                _leftFoot == null ||
                _rightUpperLeg == null ||
                _rightLowerLeg == null ||
                _rightFoot == null ||
                _leftUpperArm == null ||
                _leftLowerArm == null ||
                _leftHand == null ||
                _rightUpperArm == null ||
                _rightLowerArm == null ||
                _rightHand == null)
            {
                return false;
            }

            return true;
        }

        public void AutoPopulateBonesFromAnimator()
        {
            if (_animator == null)
            {
                SetAnimator();
            }

            if (_animator == null || !_animator.isHuman)
            {
                Debug.LogError("Animator is null or is not human! Cannot auto populate bones.", gameObject);
                return;
            }

            _hips = _animator.GetBoneTransform(HumanBodyBones.Hips);
            _spine = _animator.GetBoneTransform(HumanBodyBones.Spine);
            _chest = _animator.GetBoneTransform(HumanBodyBones.Chest);
            _head = _animator.GetBoneTransform(HumanBodyBones.Head);
            _leftUpperLeg = _animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            _leftLowerLeg = _animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            _leftFoot = _animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            _rightUpperLeg = _animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            _rightLowerLeg = _animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            _rightFoot = _animator.GetBoneTransform(HumanBodyBones.RightFoot);
            _leftUpperArm = _animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            _leftLowerArm = _animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            _leftHand = _animator.GetBoneTransform(HumanBodyBones.LeftHand);
            _rightUpperArm = _animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            _rightLowerArm = _animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            _rightHand = _animator.GetBoneTransform(HumanBodyBones.RightHand);

            _root = null;
            if (_hips != null)
            {
                var current = _hips;
                while (ContainsBone(current) && current.GetComponent<Animator>() != _animator)
                {
                    current = current.parent;
                }

                _root = current;
            }
        }

        private bool ContainsBone(Transform transform)
        {
            if (transform == null) return false;
            if (transform == _root) return true;
            if (transform == _hips) return true;
            if (transform == _spine) return true;
            if (transform == _chest) return true;
            if (transform == _head) return true;
            if (transform == _leftUpperLeg) return true;
            if (transform == _leftLowerLeg) return true;
            if (transform == _leftFoot) return true;
            if (transform == _rightUpperLeg) return true;
            if (transform == _rightLowerLeg) return true;
            if (transform == _rightFoot) return true;
            if (transform == _leftUpperArm) return true;
            if (transform == _leftLowerArm) return true;
            if (transform == _leftHand) return true;
            if (transform == _rightUpperArm) return true;
            if (transform == _rightLowerArm) return true;
            if (transform == _rightHand) return true;

            return false;
        }

        public void SetAnimator()
        {
            var animators = GetComponentsInChildren<Animator>();
            foreach (var animator in animators)
            {
                if (animator.isHuman)
                {
                    _animator = animator;
                    break;
                }
            }
        }
    }
}
