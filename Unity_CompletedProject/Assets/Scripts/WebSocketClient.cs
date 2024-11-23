using System.Collections.Concurrent;
using UnityEngine;
using WebSocketSharp;

namespace WebRTCTutorial
{
    // �޽����� �޾��� �� ȣ��� ��������Ʈ ����
    public delegate void MessageHandler(string message);

    public class WebSocketClient : MonoBehaviour
    {
        // �޽����� �޾��� �� ȣ��Ǵ� �̺�Ʈ ����
        public event MessageHandler MessageReceived;

        // WebSocket ������ �޽����� �����ϴ� �޼���
        public void SendWebSocketMessage(string message) => _ws.Send(message);

        // Unity�� Awake �޼��� - �ʱ�ȭ �۾�
        protected void Awake()
        {
            // ���� IP�� �������� ���� ��� 'localhost'�� �⺻�� ����
            var ip = string.IsNullOrEmpty(_serverIp) ? "localhost" : _serverIp;

            // WebSocket URL ���� (��: ws://localhost:8080)
            var url = $"ws://{ip}:8080";

            // WebSocket ��ü ����
            _ws = new WebSocket(url);

            // �޽��� ���� �̺�Ʈ�� ���� �̺�Ʈ�� ���� �ڵ鷯 ����
            _ws.OnMessage += OnMessage;
            _ws.OnError += OnError;

            // ������ ����
            _ws.Connect();
            // ����Ͽ��� ������ �� �Ǵ��� ����� �α� �߰�
            Debug.Log("Attempting to connect to WebSocket server at " + url);
        }

        // Unity�� Update �޼��� - �� �����Ӹ��� ȣ��
        protected void Update()
        {
            // ť�� ����� ���� �޽����� ���� �����忡�� ó��
            while (_receivedErrors.TryDequeue(out var error))
            {
                Debug.LogError("WS error: " + error); // ������ �߻��ϸ� ���
            }

            // ť�� ����� �޽����� ���� �����忡�� ó��
            while (_receivedMessages.TryDequeue(out var message))
            {
                Debug.Log("WS Message Received: " + message); // �޽����� ���ŵǸ� ���
                MessageReceived?.Invoke(message); // �̺�Ʈ �߻�
            }
        }

        // Unity�� OnDestroy �޼��� - ��ü�� �ı��� �� ȣ��
        protected void OnDestroy()
        {
            // WebSocket ��ü�� null�� ��� ����
            if (_ws == null)
            {
                return;
            }

            // �̺�Ʈ �ڵ鷯 ����
            _ws.OnMessage -= OnMessage;
            _ws.OnError -= OnError;

            // WebSocket ���� ����
            _ws.Close();
            _ws = null;
        }

        // ���� IP�� ������ �� �ִ� ���� (Inspector���� ���� ����)
        [SerializeField]
        private string _serverIp;

        // WebSocket ��ü
        private WebSocket _ws;

        // �޽����� ������ ť�� �����Ͽ� Update���� ó��
        private readonly ConcurrentQueue<string> _receivedMessages = new ConcurrentQueue<string>();
        private readonly ConcurrentQueue<string> _receivedErrors = new ConcurrentQueue<string>();

        // �޽��� ���� �� ȣ��Ǵ� �̺�Ʈ �ڵ鷯
        private void OnMessage(object sender, MessageEventArgs e)
        {
            // ���� �޽����� ť�� ����
            _receivedMessages.Enqueue(e.Data);
        }

        // ���� �߻� �� ȣ��Ǵ� �̺�Ʈ �ڵ鷯
        private void OnError(object sender, ErrorEventArgs e)
        {
            // ���� ���� �޽����� ť�� ����
            _receivedErrors.Enqueue(e.Message);
        }
    }
}
