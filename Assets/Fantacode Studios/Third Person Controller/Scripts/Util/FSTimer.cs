using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FS_Util
{

    public class FSTimer
    {
        public event Action OnCompleted;

        public float Duration { get; private set; }
        public bool IsRunning { get; private set; }
        public bool IsRepeating { get; private set; }

        float elapsed;

        public FSTimer(float durationSeconds, bool repeat = false)
        {
            Duration = durationSeconds;
            IsRepeating = repeat;
        }

        public void Start()
        {
            elapsed = 0f;
            IsRunning = true;
        }

        public void Stop()
        {
            IsRunning = false;
        }

        public void Update(float deltaTime)
        {
            if (!IsRunning)
                return;

            elapsed += deltaTime;

            if (elapsed >= Duration)
            {
                OnCompleted?.Invoke();

                if (IsRepeating)
                {
                    elapsed -= Duration;
                }
                else
                {
                    IsRunning = false;
                }
            }
        }
    }
}
