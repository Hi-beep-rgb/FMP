using UnityEditor.PackageManager;
using UnityEngine;

namespace Platformer
{
    public abstract class BaseState : IState
    {
        protected readonly PlayerScript player;
        protected readonly Animator animator;

        protected static readonly int Idle = Animator.StringToHash(name: "Idle");
        protected static readonly int Walk = Animator.StringToHash(name: "Walk");
        protected static readonly int Jump = Animator.StringToHash(name: "Jump");

        protected const float crossFadeDuration = 0.1f;

        protected BaseState(PlayerScript player, Animator animator)
        {
            this.player = player;
            this.animator = animator;
        }

        public virtual void OnEnter()
        {

        }
        public virtual void OnUpdate()
        {

        }
        public virtual void OnFixedUpdate()
        {

        }
        public virtual void OnDExit()
        { 

        }
    }
}
