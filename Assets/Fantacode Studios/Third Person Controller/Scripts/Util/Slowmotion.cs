using FS_Core;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FS_Util
{
    public class Slowmotion : MonoBehaviour
    {
        [Range(0,10)]
        public float speed = .1f;
        public AnimGraphClipInfo clipInfo;

        private void Start()
        {
            //bool a = false;
            //GetComponent<ActionExecutor>().Execute(new CharacterAction(clipInfo)
            //{
            //    animationParameters = { transitionBack = false, onAnimationUpdate = (nor, _) => { if(nor >= .8f && !a)
            //        GetComponent<ActiveRagdollSystem>().BlendToRagdoll();
            //GetComponent<ActiveRagdollSystem>().ApplyForce(HumanBodyBones.Hips, Vector3.forward * 200 * Time.deltaTime, ForceMode.Impulse);

            //a= true;} }
            //});

        }

        void Update()
        {
            if (Input.GetKeyDown("3"))
                Debug.Break();

            if (Input.GetKeyDown("1"))
                Time.timeScale = 1f;

            if (Input.GetKeyDown("2"))
                Time.timeScale = speed;
            if (Input.GetKeyDown("4"))
                Time.timeScale = .1f;

            if (Input.GetKeyDown(KeyCode.P) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }

            if (Input.GetKeyDown(KeyCode.M))
            {
                StartCoroutine(PlayAnim());
                //GetComponent<ActiveRagdollSystem>().BlendToAnimator();
                ////GetComponent<ActionExecutor>().Execute(new CharacterAction(clipInfo)
                ////{
                ////    animationParameters = { transitionBack = false }
                ////});
                ////StartCoroutine(AsyncUtil.RunAfterDelay(5, () => GetComponent<ActiveRagdollSystem>().BlendToAnimator()));
                //StartCoroutine(AsyncUtil.RunAfterDelay(5, () => {
                //    GetComponent<ActionExecutor>().Execute(new CharacterAction(clipInfo)
                //    {
                //        animationParameters = { transitionBack = false }
                //    });
                //}));
            }

            if (Input.GetKeyDown(KeyCode.L))
            {
            //    GetComponent<ActionExecutor>().Execute(new CharacterAction(clipInfo)
            //    {
            //        animationParameters = { transitionBack = false, onAnimationUpdate = (nor, _) => { bool a = false; if(nor >= .8f && !a)
            //        GetComponent<ActiveRagdollSystem>().BlendToRagdoll();
            //GetComponent<ActiveRagdollSystem>().ApplyForce(HumanBodyBones.Hips, (Vector3.forward + Vector3.up) * 100 * Time.deltaTime, ForceMode.Impulse);

            //a= true;} }
            //    });

                //GetComponent<ActiveRagdollSystem>().BlendToRagdoll(syncRoot: true);
                //GetComponent<ActiveRagdollSystem>().ApplyForce(HumanBodyBones.Hips, (Vector3.forward + Vector3.up) * 100 * Time.deltaTime, ForceMode.Impulse);
            }
        }

        IEnumerator PlayAnim()
        {
            
            GetComponent<ActiveRagdollSystem>().BlendToAnimator();
            GetComponent<ActionExecutor>().Execute(new CharacterAction(clipInfo)
            {
                animationParameters = { transitionBack = false, transitionIn = 0 }
            });
            yield return null;
        }

        private void FixedUpdate()
        {
            //if (Input.GetKeyDown(KeyCode.M))
            //{
            //    GetComponent<ActiveRagdollSystem>().ChangePivotWithoutMovingHips(GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Hips).position);
            //}
            
        }
    }
}
