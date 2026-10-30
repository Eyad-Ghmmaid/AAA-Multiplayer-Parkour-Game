using FS_Core;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace FS_Core
{

    public class HPBar : MonoBehaviour
    {
        [SerializeField] Damagable damagable;
        [SerializeField] Image breathBarImg;
        [SerializeField] TMP_Text healthTxt;
        [SerializeField] bool lookAtCamera = true;

        [SerializeField] Image healthBarImg;


        float currentFillAmountHealth = 1;
        float currentFillAmountBreath = 1;

        Camera cam;


        private void Awake()
        {
            if (damagable == null)
                damagable = GetComponentInParent<Damagable>();
        }

        private void Start()
        {
            damagable.OnHealthUpdated += ControlHealthBar;
            damagable.OnBreathUpdating += ControlBreathBar;
            cam = Camera.main;

            healthBarImg.fillAmount = damagable.CurrentHealth / damagable.MaxHealth;

            if (healthTxt != null)
                healthTxt.text = $"{Mathf.FloorToInt(damagable.CurrentHealth)}";
        }

        private void OnDestroy()
        {
            damagable.OnHealthUpdated -= ControlHealthBar;
            damagable.OnBreathUpdating -= ControlBreathBar;
        }
        private void OnDisable()
        {
            damagable.OnHealthUpdated -= ControlHealthBar;
            damagable.OnBreathUpdating -= ControlBreathBar;
        }
        void ControlHealthBar()
        {
            StartCoroutine(LerpHealth());
        }
        void ControlBreathBar()
        {
            StartCoroutine(LerpBreath());
        }

        IEnumerator LerpHealth()
        {
            var fillAmount = damagable.CurrentHealth / damagable.MaxHealth;
            while (currentFillAmountHealth > fillAmount)
            {
                fillAmount = damagable.CurrentHealth / damagable.MaxHealth;
                currentFillAmountHealth = Mathf.MoveTowards(currentFillAmountHealth, fillAmount, Time.deltaTime);
                healthBarImg.fillAmount = currentFillAmountHealth;
                yield return null;
            }
            currentFillAmountHealth = fillAmount;
            healthBarImg.fillAmount = currentFillAmountHealth;
            if (healthTxt != null)
                healthTxt.text = $"{Mathf.FloorToInt(damagable.CurrentHealth)}";
            if (damagable.CurrentHealth <= 0)
            {
                yield return new WaitForSecondsRealtime(1f);
                this.gameObject.SetActive(false);
            }
        }

        IEnumerator LerpBreath()
        {
            var fillAmount = damagable.CurrentBreath / damagable.MaxBreath;
            while (currentFillAmountBreath > fillAmount)
            {
                fillAmount = damagable.CurrentBreath / damagable.MaxBreath;
                currentFillAmountBreath = Mathf.MoveTowards(currentFillAmountBreath, fillAmount, Time.deltaTime);
                breathBarImg.fillAmount = currentFillAmountBreath;
                yield return null;
            }
            currentFillAmountBreath = fillAmount;
            breathBarImg.fillAmount = currentFillAmountBreath;
        }

        private void Update()
        {
            if (lookAtCamera && cam != null)
                transform.rotation = Quaternion.LookRotation(cam.transform.forward);
        }
    }
}