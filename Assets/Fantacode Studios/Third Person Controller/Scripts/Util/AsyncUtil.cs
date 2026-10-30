using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

namespace FS_Util
{
    public class AsyncUtil
    {
        public static IEnumerator RunAfterFrames(int numOfFrames, Action action)
        {
            for (int i = 0; i < numOfFrames; i++)
                yield return null;

            action.Invoke();
        }

        public static IEnumerator RunAfterDelay(float delay, Action action)
        {
            yield return new WaitForSeconds(delay);

            action.Invoke();
        }

        public static async Task RunAfterFramesAsync(int numOfFrames, Action action)
        {
            for (int i = 0; i < numOfFrames; i++)
            {
                await Task.Yield();
            }

            action.Invoke();
        }
        public static async Task RunAfterDelayAsync(float delay, Action action)
        {
            await Task.Delay(TimeSpan.FromSeconds(delay));

            action.Invoke();
        }
    }
}