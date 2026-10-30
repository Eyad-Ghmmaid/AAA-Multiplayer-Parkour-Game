using FS_Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FS_Core
{

    public class DamageTester : MonoBehaviour
    {
        Damagable damagable;

        private void Start()
        {
            damagable = GetComponent<Damagable>();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.T))
            {
                damagable.UpdateHealth(-5);
            }
        }
    }

}
