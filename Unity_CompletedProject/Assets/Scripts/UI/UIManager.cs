using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UI;
using UnityEngine.Android;
using Unity.WebRTC;

namespace WebRTCTutorial.UI
{
    public class UIManager : MonoBehaviour
    {
#if UNITY_EDITOR
        // Called by Unity -> https://docs.unity3d.com/ScriptReference/MonoBehaviour.OnValidate.html
        protected void OnValidate()
        {
            try
            {
                // Validate that all references are connected
                Assert.IsNotNull(_peerViewA);
                Assert.IsNotNull(_peerViewB);
                Assert.IsNotNull(_cameraDropdown);
                Assert.IsNotNull(_connectButton);
                Assert.IsNotNull(_disconnectButton);
            }
            catch (Exception)
            {
                Debug.LogError(
                    $"Some of the references are NULL, please inspect the {nameof(UIManager)} script on this object",
                    this);
            }
        }
#endif

        // Called by Unity -> https://docs.unity3d.com/ScriptReference/MonoBehaviour.Awake.html
        protected void Awake()
        {
            Debug.Log($"UiManager Awake");
            // FindObjectOfType is used for the demo purpose only. In a real production it's better to avoid it for performance reasons
            _videoManager = FindObjectOfType<VideoManager>();

            // Android에서 카메라 권한 요청
            if (Application.platform == RuntimePlatform.Android)
            {
                RequestCameraPermission();
            }

            // Check if there's any camera device available
            if (WebCamTexture.devices.Length == 0)
            {
                Debug.LogError(
                    "No Camera devices available! Please make sure a camera device is detected and accessible by Unity. " +
                    "This demo application will not work without a camera device.");
            }

            // Subscribe to buttons
            _connectButton.onClick.AddListener(OnConnectButtonClicked);
            _disconnectButton.onClick.AddListener(OnDisconnectButtonClicked);

            // Clear default options from the dropdown
            _cameraDropdown.ClearOptions();

            // Populate dropdown with the available camera devices
            foreach (var cameraDevice in WebCamTexture.devices)
            {
                _cameraDropdown.options.Add(new TMP_Dropdown.OptionData(cameraDevice.name));
            }

            // Change the active camera device when new dropdown value is selected
            _cameraDropdown.onValueChanged.AddListener(SetActiveCamera);

            // Subscribe to when video from the other peer is received
            _videoManager.RemoteVideoReceived += OnRemoteVideoReceived;
            Debug.Log($"UiManager Awake");
        }

        // Called by Unity -> https://docs.unity3d.com/ScriptReference/MonoBehaviour.Start.html
        protected void Start()
        {
            // Enable first camera from the dropdown.
            // We call it in Start to make sure that Awake of all game objects completed and all scripts 
            SetActiveCamera(deviceIndex: 0);
        }

        // Called by Unity -> https://docs.unity3d.com/ScriptReference/MonoBehaviour.Update.html
        protected void Update()
        {
            // Control buttons being clickable by the connection state
            _connectButton.interactable = _videoManager.CanConnect;
            _disconnectButton.interactable = _videoManager.IsConnected;
        }

        [SerializeField]
        private PeerView _peerViewA;

        [SerializeField]
        private PeerView _peerViewB;

        [SerializeField]
        private TMP_Dropdown _cameraDropdown;

        [SerializeField]
        private Button _connectButton;

        [SerializeField]
        private Button _disconnectButton;

        private WebCamTexture _activeCamera;

        private VideoManager _videoManager;

        private RTCPeerConnection _peerConnection; // Add the peer connection variable

        // Android에서 카메라 권한을 요청하는 메서드
        private void RequestCameraPermission()
        {
            if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                Permission.RequestUserPermission(Permission.Camera);
            }
        }

        private void SetActiveCamera(int deviceIndex)
        {
            var deviceName = _cameraDropdown.options[deviceIndex].text;

            // Stop previous camera capture
            if (_activeCamera != null && _activeCamera.isPlaying)
            {
                _activeCamera.Stop();
            }

            // Some platforms (like Android) require 16x16 alignment for the texture size to be sent via WebRTC
            _activeCamera = new WebCamTexture(deviceName, 1024, 768, requestedFPS: 30);

            _activeCamera.Play();

            // starting the camera might fail if the device is not accessible (e.g. used by another application)
            if (!_activeCamera.isPlaying)
            {
                Debug.LogError($"Failed to start the {deviceName} camera device.");
                return;
            }

            StartCoroutine(PassActiveCameraToVideoManager());
        }

        /// <summary>
        /// Starting the camera is an asynchronous operation.
        /// If we create the video track before camera is active it may have an invalid resolution.
        /// Therefore, it's best to wait until camera is in fact started before passing it to the video track
        /// </summary>
        private IEnumerator PassActiveCameraToVideoManager()
        {
            var timeElapsed = 0f;
            while (!_activeCamera.didUpdateThisFrame)
            {
                yield return null;

                // infinite loop prevention
                timeElapsed += Time.deltaTime;
                if (timeElapsed > 5f)
                {
                    Debug.LogError("Camera didn't start after 5 seconds. Aborting. The video track is not created.");
                    yield break;
                }
            }

            // Set preview of the local peer (Peer A) with the original camera texture
            _peerViewA.SetVideoTexture(_activeCamera);

            // Set preview of the remote peer (Peer B) with the original camera texture
            // _peerViewB.SetVideoTexture( /* Remote Video Texture */ );

            // Rotate PeerView A and PeerView B GameObjects by 90 degrees on Y-axis
            _peerViewA.transform.rotation = Quaternion.Euler(0, 0, 90); // Rotate 90 degrees on Y-axis
            _peerViewB.transform.rotation = Quaternion.Euler(0, 0, 90); // Rotate 90 degrees on Y-axis

            // Notify Video Manager about new active camera device
            _videoManager.SetActiveCamera(_activeCamera);

            // Check if peerConnection is null before proceeding
            if (_peerConnection == null)
            {
                Debug.LogWarning("Peer connection is null. Initializing peer connection now.");
                _peerConnection = new RTCPeerConnection(); // Initialize the peer connection here if null
            }
        }

        private void OnRemoteVideoReceived(Texture texture)
        {
            // OnRemoteVideoReceived에서 받은 텍스처를 그대로 전달
            _peerViewB.SetVideoTexture(texture);

            // 피어 B의 게임 오브젝트도 90도 회전
            _peerViewB.transform.rotation = Quaternion.Euler(0, 90, 0); // Rotate 90 degrees on Y-axis
        }

        private void OnConnectButtonClicked()
        {
            // Ensure peerConnection is initialized
            if (_peerConnection == null)
            {
                Debug.LogWarning("Peer connection is null, initializing fuck");
                _peerConnection = new RTCPeerConnection(); // Ensure the peer connection is initialized
                Debug.LogWarning("go go!!");
            }

            // Now try to connect
            if (_videoManager.CanConnect)
            {
                Debug.LogWarning("connect");
                _videoManager.Connect();  // Assuming _videoManager handles the actual connection logic
            }
            else
            {
                Debug.LogWarning("Cannot connect, please check the peer connection status.");
            }
        }


        private void OnDisconnectButtonClicked()
        {
            _videoManager.Disconnect();
        }
    }
}
