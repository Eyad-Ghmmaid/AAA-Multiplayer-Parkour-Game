using FS_Core;
using FS_LocomotionSystem;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


namespace FS_Core
{
    public class FootStepEffects : MonoBehaviour
    {
        [SerializeField] bool enableFootStepEffect = true;
        [SerializeField] List<AudioClip> footStepSounds;
        [SerializeField] List<GameObject> footStepParticles;

        [SerializeField] List<OverrideStepEffects> overrideStepEffects;
        [SerializeField] StepEffectsOverrideType overrideType;

        public LayerMask groundLayer = 1;

        [SerializeField] List<SystemState> soundIgnoreStates;
        [SerializeField] List<SystemState> particleIgnoreStates;

        [SerializeField] bool adjustVolumeBasedOnSpeed = true;
        [SerializeField] float minVolume = 0.2f;

        [Tooltip("Normalized speed (0-1) the character must exceed to emit a footstep game sound event.")]
        [SerializeField] float speedThresholdForEmittingSound = 0.5f;

        [Tooltip("Footstep loudness multiplier at full crouch (e.g. 0.5 = 50% quieter footsteps and reduced game sound radius). Scales smoothly with crouch depth; standing = no reduction.")]
        [Range(0f, 1f)]
        [SerializeField] float crouchVolumeMultiplier = 0.5f;
        [SerializeField] float footstepSoundRadius = 15f;

        public List<OverrideStepEffects> OverrideStepEffects => overrideStepEffects;
        public StepEffectsOverrideType OverrideType => overrideType;

        PlayerController playerController;
        Animator animator;
        IMotionDriver motionDriver;
        LocomotionCore locomotionCore;

        private void Awake()
        {
            playerController = GetComponent<PlayerController>();
            animator = GetComponent<Animator>();
            motionDriver = GetComponent<IMotionDriver>();
            locomotionCore = GetComponent<LocomotionCore>();
        }

        // Normalized (0-1) horizontal speed of the character based on its exact velocity,
        // using the locomotion core's sprint speed as the full-volume reference.
        float GetNormalizedSpeed()
        {
            if (motionDriver == null || locomotionCore == null)
                return 0f;

            Vector3 velocity = motionDriver.GetVelocity();
            float horizontalSpeed = new Vector3(velocity.x, 0f, velocity.z).magnitude;
            return horizontalSpeed / locomotionCore.SprintSpeed;
        }

        // Volume multiplier for the current crouch depth. Reads the actual crouch blend (idleType,
        // which ranges 0 = standing .. 0.5 = fully crouched) 
        float GetCrouchVolumeFactor()
        {
            if (animator == null)
                return 1f;

            float crouchBlend = Mathf.Clamp01(animator.GetFloat(AnimatorParameters.idleType) / 0.5f);
            return Mathf.Lerp(1f, crouchVolumeMultiplier, crouchBlend);
        }

        private void Start()
        {
            int footTriggerLayer = LayerMask.NameToLayer("FootTrigger");

            for (int i = 0; i < 32; i++)
            {
                Physics.IgnoreLayerCollision(footTriggerLayer, i, !IsLayerInLayerMask(i, groundLayer));
            }

            if (overrideStepEffects != null)
            {
                overrideStepEffects.ForEach(x => x.MaterialName = x.MaterialName.ToLower());
            }
        }

        public void OnFootLand(Transform footTransform, FloorStepData floorData = null)
        {
            if(!enableFootStepEffect)
                return;
            var sounds = footStepSounds;
            var particleEffects = footStepParticles;

            if (floorData != null && overrideStepEffects != null)
            {
                //Debug.Log(floorData.materialName + " - " + floorData.textureName + " - " + floorData.tag);

                var overrideEffect = GetOverrideEffect(floorData);
                if (overrideEffect != null)
                {
                    if (overrideEffect.ovverideFootStepSounds)
                        sounds = overrideEffect.footStepSounds;

                    if (overrideEffect.overrideFootStepParticles)
                        particleEffects = overrideEffect.footStepParticles;
                }
            }

            if(sounds != null && sounds.Count > 0)
            {
                if (playerController != null && !soundIgnoreStates.Contains(playerController.FocusedSystemState))
                {
                    float normalizedSpeed = GetNormalizedSpeed() * GetCrouchVolumeFactor();
                    PlaySfx(sounds[Random.Range(0, sounds.Count)], normalizedSpeed);

                    if (normalizedSpeed > speedThresholdForEmittingSound)
                    {
                        GameSoundEvents.Emit(new GameSoundEvent()
                        {
                            position = transform.position,
                            radius = footstepSoundRadius * normalizedSpeed,
                            type = GameSoundType.Footstep
                        });
                    }
                }
                else if (playerController == null)
                    PlaySfx(sounds[Random.Range(0, sounds.Count)]);
            }

            if (particleEffects != null && particleEffects.Count > 0)
            {
                if(playerController != null && !particleIgnoreStates.Contains(playerController.FocusedSystemState))
                    SpawnParticle(particleEffects[Random.Range(0, particleEffects.Count)], footTransform);
                else if(playerController == null)
                    SpawnParticle(particleEffects[Random.Range(0, particleEffects.Count)], footTransform);
            }
        }

        void PlaySfx(AudioClip clip, float volume = 1)
        {
            GameObject sfx = new GameObject();
            sfx.transform.position = transform.position;
            var audioSource = sfx.AddComponent<AudioSource>();
            audioSource.clip = clip;

            if (adjustVolumeBasedOnSpeed)
                audioSource.volume = Mathf.Clamp(volume, minVolume, audioSource.volume);

            audioSource.Play();
            Destroy(sfx, 1.5f);
        }

        void SpawnParticle(GameObject particleEffect, Transform footTransform)
        {
            var particleObj = Instantiate(particleEffect, footTransform.position, footTransform.rotation);
            Destroy(particleObj, 2f);
        }

        OverrideStepEffects GetOverrideEffect(FloorStepData floorData)
        {
            if (overrideType == StepEffectsOverrideType.MaterialName)
                return overrideStepEffects.FirstOrDefault(x => x.MaterialName == floorData.materialName);
            else if (overrideType == StepEffectsOverrideType.TextureName)
                return overrideStepEffects.FirstOrDefault(x => x.TextureName == floorData.textureName);
            else
                return overrideStepEffects.FirstOrDefault(x => x.Tag == floorData.tag);
        }

        bool IsLayerInLayerMask(int layer, LayerMask layerMask)
        {
            return (layerMask.value & (1 << layer)) != 0;
        }

        public LayerMask GroundLayer => groundLayer;

        public void EnableFootStepEffects(bool enable) => enableFootStepEffect = enable;
        public bool IsEnabled => enableFootStepEffect;
    }

    [System.Serializable]
    public class OverrideStepEffects
    {
        public string tag;
        public Material materialName;
        public Texture textureName;

        public bool ovverideFootStepSounds;
        public bool overrideFootStepParticles;

        public List<AudioClip> footStepSounds;
        public List<GameObject> footStepParticles;

        public string Tag => tag;
        public string MaterialName { get { return materialName.name; } set { materialName.name = value; } } 
        public string TextureName { get { return textureName.name; } set { textureName.name = value; } }
       
    }

    public enum StepEffectsOverrideType { MaterialName, TextureName, Tag }
}