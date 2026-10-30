using System.Collections.Generic;
using UnityEngine;
using Fusion;
using FantacodeStudios.ThirdPersonController;

namespace AvocadoShark
{
    public class PlayerLocalActivator : NetworkBehaviour
    {
        [Header("Local-only GameObjects (Camera, AudioListener, Local HUD etc.)")]
        public List<GameObject> localOnlyGameObjects;

        [Header("Local-only Behaviour components (Input scripts, controllers etc.)")]
        public List<UnityEngine.Behaviour> localOnlyBehaviours;

        [Header("World-space HUD (healthbar etc.) - keep active for everyone if null")]
        public GameObject worldSpaceHUD;

        public override void Spawned()
        {
            base.Spawned();
            bool isLocal = Object.HasInputAuthority || Object.HasStateAuthority;
            SetLocalState(isLocal);

            if (isLocal && FusionConnection.Instance != null)
            {
                var runner = FusionConnection.Instance.Runner;
                if (runner != null && runner.IsConnectedToServer)
                {
                    runner.SetPlayerObject(runner.LocalPlayer, Object);
                }
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            base.Despawned(runner, hasState);
            // clean up if needed
        }

        private void SetLocalState(bool isLocal)
        {
            if (localOnlyGameObjects != null)
            {
                for (int i = 0; i < localOnlyGameObjects.Count; i++)
                {
                    var go = localOnlyGameObjects[i];
                    if (go != null)
                        go.SetActive(isLocal);
                }
            }

            if (localOnlyBehaviours != null)
            {
                for (int i = 0; i < localOnlyBehaviours.Count; i++)
                {
                    var b = localOnlyBehaviours[i];
                    if (b != null)
                        b.enabled = isLocal;
                }
            }

            // FSInputAdapter must only run on the local player (it reads hardware input)
            var fsInputAdapter = GetComponent<FSInputAdapter>();
            if (fsInputAdapter != null)
                fsInputAdapter.enabled = isLocal;

            // FSNetworkInputBridge must only run on the local player
            var networkInputBridge = GetComponent<FSNetworkInputBridge>();
            if (networkInputBridge != null)
                networkInputBridge.enabled = isLocal;

            if (worldSpaceHUD != null)
            {
                // keep world-space HUD visible for everyone; override if you want it local-only
                worldSpaceHUD.SetActive(true);
            }

            // Inform UI manager if this is the local player
            if (isLocal && GameUIManager.Instance != null)
            {
                GameUIManager.Instance.SetLocalPlayer(this);
            }
        }
    }
}
