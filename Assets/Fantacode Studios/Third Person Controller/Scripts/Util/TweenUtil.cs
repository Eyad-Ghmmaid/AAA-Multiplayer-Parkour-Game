using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FS_Util
{
    public class TweenUtil
    {
        public static void TweenPosition(GameObject target, Vector3 endPos, float duration=1f, 
            Action onComplete=null)
        {
            target.GetComponent<MonoBehaviour>().StartCoroutine(TweenPositionAsync(target, endPos, duration));
        }

        public static IEnumerator TweenPositionAsync(GameObject target, Vector3 endPos, float duration=1f, 
            Action onComplete=null)
        {
            var transform = target.transform;

            Vector3 startPos = transform.position;

            float elapsedTime = 0f;
            while (elapsedTime < duration)
            {
                transform.position = Vector3.Lerp(startPos, endPos, elapsedTime / duration);
                elapsedTime += Time.unscaledDeltaTime;
                yield return null;
            }
            transform.position = endPos;

            onComplete?.Invoke();
        }

        public static IEnumerator TweenAlphaAsync(Image image, float targetAlpha, float duration = 1f,
            Action onComplete = null)
        {
            image.gameObject.SetActive(true);
            float startAlpha = image.color.a;

            float elapsedTime = 0f;
            while (elapsedTime < duration)
            {
                image.color = new Color(image.color.r, image.color.g, image.color.b, Mathf.Lerp(startAlpha, targetAlpha, elapsedTime / duration));
                elapsedTime += Time.unscaledDeltaTime;
                yield return null;
            }

            image.color = new Color(image.color.r, image.color.g, image.color.b, targetAlpha);

            onComplete?.Invoke();
        }

        public static IEnumerator TweenVal(float start, float end, float duration, Action<float> onLerp)
        {
            float timer = 0f;
            float percent = timer / duration;

            while (percent <= 1f)
            {
                timer += Time.deltaTime;
                percent = timer / duration;
                var lerpVal = Mathf.Lerp(start, end, percent);
                onLerp?.Invoke(lerpVal);

                yield return null;
            }
        }
    }
}
