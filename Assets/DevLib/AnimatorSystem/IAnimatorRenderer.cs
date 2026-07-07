using System;
using UnityEngine;

namespace DevLib.AnimatorSystem
{
    public interface IAnimatorRenderer
    {
        public Animator Animator { get; }
        event Action OnAnimationEnd;
        event Action OnDamageCast;
        event Action<Vector2> OnFootstep;
        void RenderClip(int clipHash);
        void RenderClipIfNotPlaying(int clipHash);
        void SetMovementDirection(Vector2 dir);
        
        Vector2 GetFacingDirection();
    }
}