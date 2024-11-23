using System;
using System.Collections;
using Unity.WebRTC;
using UnityEngine;
using UnityEngine.Android;
using WebRTCTutorial.DTO;

namespace WebRTCTutorial
{
    public class VideoManager : MonoBehaviour
    {
        public event Action<Texture> RemoteVideoReceived;
        public bool CanConnect = true;
        public bool IsConnected => _peerConnection?.ConnectionState == RTCPeerConnectionState.Connecting;

        private WebSocketClient _webSocketClient;
        private RTCPeerConnection _peerConnection;

        // Android에서 카메라 권한을 요청하는 메서드 추가
        private void RequestCameraPermission()
        {
            if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                Permission.RequestUserPermission(Permission.Camera);
            }
        }

        // Unity의 Awake 메서드에서 카메라 권한 요청 추가
        protected void Awake()
        {
            // 필요에 따라 코덱을 초기화
      

            Debug.Log("Awake Called");

            // Android에서 카메라 권한 요청
            if (Application.platform == RuntimePlatform.Android)
            {
                RequestCameraPermission();
            }

            // WebSocketClient 찾기 (FindObjectOfType는 데모용으로 사용)
            _webSocketClient = FindObjectOfType<WebSocketClient>();

            StartCoroutine(WebRTC.Update());

            // RTCConfiguration 설정
            var config = new RTCConfiguration
            {
                iceServers = new RTCIceServer[]
                {
                    new RTCIceServer
                    {
                        urls = new string[] { "stun:stun.l.google.com:19302" } // Google STUN 서버
                    }
                },
            };
            Debug.Log($"videoManager");
            // _peerConnection 생성
            _peerConnection = new RTCPeerConnection(ref config);
            if (_peerConnection == null)
            {
                Debug.LogError("Failed to create RTCPeerConnection.");
                return;
            }

            Debug.Log($"Initial ConnectionState: {_peerConnection.ConnectionState}");
            Debug.Log($"videoManagerAwake");
            // 이벤트 핸들러 설정
            _peerConnection.OnNegotiationNeeded += OnNegotiationNeeded;
            _peerConnection.OnIceCandidate += OnIceCandidate;
            _peerConnection.OnTrack += OnTrack;
            _webSocketClient.MessageReceived += OnWebSocketMessageReceived;
        }

        // SetActiveCamera: WebCamTexture를 사용하여 카메라 트랙을 설정
        public void SetActiveCamera(WebCamTexture activeWebCamTexture)
        {
            // 이전 트랙 제거
            var senders = _peerConnection.GetSenders();
            foreach (var sender in senders)
            {
                _peerConnection.RemoveTrack(sender);
            }

            // 새로운 비디오 트랙 추가
            var videoTrack = new VideoStreamTrack(activeWebCamTexture);
            _peerConnection.AddTrack(videoTrack);

            Debug.Log("Sender video track was set");
        }

        // 연결을 시작하는 메서드
        public void Connect()
        {
            // _peerConnection이 null이라면 초기화
            if (_peerConnection == null)
            {
                Debug.LogError("_peerConnection is null, attempting to reinitialize.");
                var config = new RTCConfiguration
                {
                    iceServers = new RTCIceServer[]
                    {
                        new RTCIceServer { urls = new string[] { "stun:stun.l.google.com:19302" } }
                    }
                };

                _peerConnection = new RTCPeerConnection(ref config);
                if (_peerConnection == null)
                {
                    Debug.LogError("Failed to reinitialize RTCPeerConnection.");
                    return;
                }
            }

            // 연결을 시도하기 전에 _peerConnection이 null이 아닌지 확인
            if (_peerConnection != null)
            {
                StartCoroutine(CreateAndSendLocalSdpOffer());
            }
            else
            {
                Debug.LogError("_peerConnection is null after reinitialization.");
            }
        }

        // 연결을 종료하는 메서드
        public void Disconnect()
        {
            if (!IsConnected)
            {
                return;
            }

            _peerConnection.Close();
            _peerConnection.Dispose();
            _peerConnection = null; // Disconnect 후 _peerConnection을 null로 설정
        }

        // WebRTC 이벤트 핸들러들
        private void OnTrack(RTCTrackEvent trackEvent)
        {
            if (trackEvent.Track is VideoStreamTrack videoStreamTrack)
            {
                videoStreamTrack.OnVideoReceived += OnVideoReceived;
            }
            else
            {
                Debug.LogError($"Unhandled track of type: {trackEvent.Track.GetType()}. Only video tracks are handled.");
            }
        }

        private void OnVideoReceived(Texture texture)
        {
            Debug.Log($"Video received, resolution: {texture.width}x{texture.height}");
            RemoteVideoReceived?.Invoke(texture);
        }

        private void OnNegotiationNeeded()
        {
            Debug.Log("Negotiation needed.");
        }

        private void OnIceCandidate(RTCIceCandidate candidate)
        {
            SendIceCandidateToOtherPeer(candidate);
            Debug.Log("Sent Ice Candidate to the other peer");
        }

        private void OnWebSocketMessageReceived(string message)
        {
            var dtoWrapper = JsonUtility.FromJson<DTOWrapper>(message);
            switch ((DtoType)dtoWrapper.Type)
            {
                case DtoType.ICE:
                    var iceDto = JsonUtility.FromJson<ICECanddidateDTO>(dtoWrapper.Payload);
                    var ice = new RTCIceCandidate(new RTCIceCandidateInit
                    {
                        candidate = iceDto.Candidate,
                        sdpMid = iceDto.SdpMid,
                        sdpMLineIndex = iceDto.SdpMLineIndex
                    });

                    _peerConnection.AddIceCandidate(ice);
                    Debug.Log($"Received ICE Candidate: {ice.Candidate}");
                    break;
                case DtoType.SDP:
                    var sdpDto = JsonUtility.FromJson<SdpDTO>(dtoWrapper.Payload);
                    var sdp = new RTCSessionDescription
                    {
                        type = (RTCSdpType)sdpDto.Type,
                        sdp = sdpDto.Sdp
                    };

                    Debug.Log($"Received SDP offer of type: {sdp.type} and SDP details: {sdp.sdp}");

                    switch (sdp.type)
                    {
                        case RTCSdpType.Offer:
                            StartCoroutine(OnRemoteSdpOfferReceived(sdp));
                            break;
                        case RTCSdpType.Answer:
                            StartCoroutine(OnRemoteSdpAnswerReceived(sdp));
                            break;
                        default:
                            throw new ArgumentOutOfRangeException($"Unhandled type of SDP message: {sdp.type}");
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void SendIceCandidateToOtherPeer(RTCIceCandidate iceCandidate)
        {
            var iceDto = new ICECanddidateDTO
            {
                Candidate = iceCandidate.Candidate,
                SdpMid = iceCandidate.SdpMid,
                SdpMLineIndex = iceCandidate.SdpMLineIndex
            };

            SendMessageToOtherPeer(iceDto, DtoType.ICE);
        }

        private void SendSdpToOtherPeer(RTCSessionDescription sdp)
        {
            var sdpDto = new SdpDTO
            {
                Type = (int)sdp.type,
                Sdp = sdp.sdp
            };

            SendMessageToOtherPeer(sdpDto, DtoType.SDP);
        }

        private void SendMessageToOtherPeer<TType>(TType obj, DtoType type)
        {
            if (_webSocketClient == null)
            {
                Debug.LogError("_webSocketClient is null.");
                return;
            }

            try
            {
                var serializedPayload = JsonUtility.ToJson(obj);

                var dtoWrapper = new DTOWrapper
                {
                    Type = (int)type,
                    Payload = serializedPayload
                };

                var serializedDto = JsonUtility.ToJson(dtoWrapper);

                _webSocketClient.SendWebSocketMessage(serializedDto);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private IEnumerator CreateAndSendLocalSdpOffer()
        {
            if (_peerConnection == null)
            {
                Debug.LogError("_peerConnection is null.");
                yield break;
            }

            // 1. 로컬 SDP offer 생성
            var createOfferOperation = _peerConnection.CreateOffer();
            yield return createOfferOperation;

            if (createOfferOperation.IsError)
            {
                Debug.LogError("Failed to create offer");
                yield break;
            }

            var sdpOffer = createOfferOperation.Desc;

            // 2. offer를 로컬 SDP로 설정
            var setLocalDescOperation = _peerConnection.SetLocalDescription(ref sdpOffer);
            yield return setLocalDescOperation;

            if (setLocalDescOperation.IsError)
            {
                Debug.LogError("Failed to set local description");
                yield break;
            }

            // 3. 상대방에게 offer 전송
            SendSdpToOtherPeer(sdpOffer);
        }

        private IEnumerator OnRemoteSdpOfferReceived(RTCSessionDescription sdp)
        {
            if (_peerConnection == null)
            {
                Debug.LogError("_peerConnection is null.");
                yield break;
            }

            var setRemoteDescOperation = _peerConnection.SetRemoteDescription(ref sdp);
            yield return setRemoteDescOperation;

            if (setRemoteDescOperation.IsError)
            {
                Debug.LogError("Failed to set remote description");
                yield break;
            }

            // 응답을 위한 SDP answer 생성
            var createAnswerOperation = _peerConnection.CreateAnswer();
            yield return createAnswerOperation;

            if (createAnswerOperation.IsError)
            {
                Debug.LogError("Failed to create answer");
                yield break;
            }

            var sdpAnswer = createAnswerOperation.Desc;

            // 3. answer를 로컬 SDP로 설정
            var setLocalDescOperation = _peerConnection.SetLocalDescription(ref sdpAnswer);
            yield return setLocalDescOperation;

            if (setLocalDescOperation.IsError)
            {
                Debug.LogError("Failed to set local description");
                yield break;
            }

            // 4. 상대방에게 answer 전송
            SendSdpToOtherPeer(sdpAnswer);
        }

        private IEnumerator OnRemoteSdpAnswerReceived(RTCSessionDescription sdp)
        {
            if (_peerConnection == null)
            {
                Debug.LogError("_peerConnection is null.");
                yield break;
            }

            var setRemoteDescOperation = _peerConnection.SetRemoteDescription(ref sdp);
            yield return setRemoteDescOperation;

            if (setRemoteDescOperation.IsError)
            {
                Debug.LogError("Failed to set remote description");
                yield break;
            }
        }
    }
}
