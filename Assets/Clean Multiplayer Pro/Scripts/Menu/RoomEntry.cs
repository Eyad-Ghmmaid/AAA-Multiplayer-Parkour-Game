#if CMPSETUP_COMPLETE
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using Fusion;

namespace AvocadoShark
{
    public class RoomEntry : MonoBehaviour
    {
        [Header("Joining")] public Button joinButton;
        public CanvasGroup canvasGroup;
        public float lerpSpeed = 0.5f; // Speed for fade in. Adjust as needed.
        public int currentPlayers, maxPlayers;

        /// <summary>The session this entry represents. Read-only to callers.</summary>
        public SessionInfo SessionInfo { get; private set; }

        /// <summary>True when this room asks for a password before joining.</summary>
        public bool HasPassword { get; private set; }


        public Color flashColor = Color.red;
        public float flashDuration = 1.0f;

        public TextMeshProUGUI roomName, playerCount;
        public FusionConnection fusionConnectionRef;

        [Header("Password")] [SerializeField] private GameObject passwordPanel;
        [SerializeField] private TMP_InputField passwordInput;
        [SerializeField] private Button acceptPasswordbutton;

        private Color playerCountColor, passwordInputColor;

        Coroutine RoomIsFullIndicationRoutine, WrongPasswordIndicationRoutine;

        public void Start()
        {
            playerCountColor = playerCount.color;
            passwordInputColor = passwordInput.textComponent.color;
        }

        public void Init(SessionInfo session,FusionConnection fusionConnection)
        {
            transform.localScale = Vector3.one;
            fusionConnection.OnDestroyRoomEntries += DestroyEntry;
            ApplySessionInfo(session, fusionConnection);
        }

        public void UpdateEntry(SessionInfo session,FusionConnection fusionConnection)
        {
            ApplySessionInfo(session, fusionConnection);
        }

        private void ApplySessionInfo(SessionInfo session, FusionConnection fusionConnection)
        {
            SessionInfo = session;
            roomName.text = session.Name;
            currentPlayers = session.PlayerCount;
            maxPlayers = session.MaxPlayers;
            playerCount.text = session.PlayerCount + "/" + session.MaxPlayers;
            joinButton.interactable = session.IsOpen;
            fusionConnectionRef = fusionConnection;
            // Only whether a password is required is kept here - never the password or its
            // hash, and it is never logged.
            HasPassword = RoomPassword.IsProtected(session);
        }

        public void JoinRoom()
        {
            if (currentPlayers >= maxPlayers)
            {
                RoomIsFullIndication();
                return;
            }
            PlayerPrefs.SetInt("has_pass", 0);
            if (!HasPassword)
            {
                fusionConnectionRef.loadingScreenScript.gameObject.SetActive(true);
               // Invoke(nameof(ContinueJoinRoom), fusionConnectionRef.loadingScreenScript.lerpSpeed);
               ContinueJoinRoom();
            }
            else
            {
                EnablePasswordPanel();
                joinButton.gameObject.SetActive(false);
            }
        }

        public void AcceptPassword()
        {
            if (currentPlayers >= maxPlayers)
            {
                RoomIsFullIndication();
                DisablePasswordPanel();
                joinButton.gameObject.SetActive(true);
                return;
            }

            if (_verifyRoutine == null)
                _verifyRoutine = StartCoroutine(VerifyAndJoin(passwordInput.text));
        }

        private Coroutine _verifyRoutine;

        private IEnumerator VerifyAndJoin(string attempt)
        {
            // Checking the password derives a key, which blocks the main thread for a
            // noticeable moment (longer in a browser). Show the button as busy and let the
            // UI paint first, otherwise a wrong guess just looks like the game froze.
            if (acceptPasswordbutton != null)
                acceptPasswordbutton.interactable = false;
            yield return null;

            var accepted = RoomPassword.Verify(SessionInfo, attempt);

            if (acceptPasswordbutton != null)
                acceptPasswordbutton.interactable = true;
            _verifyRoutine = null;

            if (accepted)
            {
                passwordInput.text = string.Empty; // don't leave it sitting in the UI
                fusionConnectionRef.loadingScreenScript.gameObject.SetActive(true);
                PlayerPrefs.SetInt("has_pass", 1);
                ContinueJoinRoom();
            }
            else
            {
                WrongPasswordIndication();
            }
        }

        
        private void ContinueJoinRoom()
        {
            // Join by the session's real name, not the UI label - the label can be
            // decorated or truncated, and the two are deliberately different for
            // unlisted rooms.
            fusionConnectionRef.JoinRoom(SessionInfo.Name);
        }

        private void EnablePasswordPanel()
        {
            SetTryingToJoinRoom();
            passwordPanel.SetActive(true);
        }

        private void DisablePasswordPanel()
        {
            SetQuitTryingToJoinRoom();
            passwordPanel.SetActive(false);
        }

        public void RoomIsFullIndication()
        {
            if (RoomIsFullIndicationRoutine != null)
            {
                StopCoroutine(RoomIsFullIndicationRoutine);
                playerCount.color = playerCountColor;
            }

            RoomIsFullIndicationRoutine = StartCoroutine(FlashPlayerNumberText());
        }

        public void WrongPasswordIndication()
        {
            if (WrongPasswordIndicationRoutine != null)
            {
                StopCoroutine(WrongPasswordIndicationRoutine);
                passwordInput.textComponent.color = passwordInputColor;
            }

            WrongPasswordIndicationRoutine = StartCoroutine(FlashPasswordText());
        }

        public void DestroyEntry()
        {
            FusionConnection.Instance.OnDestroyRoomEntries -= DestroyEntry;
            Destroy(gameObject);
        }

        IEnumerator FlashPlayerNumberText()
        {
            playerCount.color = flashColor;
            yield return new WaitForSeconds(flashDuration / 2);

            playerCount.color = playerCountColor;
            yield return new WaitForSeconds(flashDuration / 2);
        }

        private IEnumerator FlashPasswordText()
        {
            passwordInput.textComponent.color = flashColor;
            yield return new WaitForSeconds(flashDuration / 2);

            passwordInput.textComponent.color = passwordInputColor;
            yield return new WaitForSeconds(flashDuration / 2);
        }

        public void OnDisable()
        {
            joinButton.onClick.RemoveListener(JoinRoom);
        }

        public void SetTryingToJoinRoom()
        {
            FusionConnection.Instance.SetCurrentEntryBeingEdited(this);
            FusionConnection.Instance.OnDestroyRoomEntries -= DestroyEntry;
        }

        public void SetQuitTryingToJoinRoom()
        {
            FusionConnection.Instance.ResetCurrentEntryBeingEdited(this);
        }
    }
}
#endif