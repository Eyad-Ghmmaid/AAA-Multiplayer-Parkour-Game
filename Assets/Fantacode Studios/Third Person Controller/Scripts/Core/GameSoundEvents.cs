using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FS_Core
{
    public class GameSoundEvents
    {
        public static event Action<GameSoundEvent> OnSoundEmitted;

        public static void Emit(GameSoundEvent evt)
        {
            OnSoundEmitted?.Invoke(evt);
        }
    }

    public enum GameSoundType
    {
        Gunshot,
        Footstep,
        ThrowImpact,
        Explosion
    }

    public class GameSoundEvent
    {
        public Vector3 position;
        public float radius;
        public GameSoundType type;
        public float baseLoudness = 1f;
        public GameObject source;
    }
}
