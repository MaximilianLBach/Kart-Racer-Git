using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace Kart {
    public class NetworkStartUI : MonoBehaviour {
        [SerializeField] Button startHostButton;
        [SerializeField] Button startClientButton;
        
        void Start() {
            startHostButton.onClick.AddListener(StartHost);
            startClientButton.onClick.AddListener(StartClient);
        }

        public void StartHost() {
            Debug.Log("Starting host");
            NetworkManager.Singleton.StartHost();
            Hide();
        }

        public void StartClient() {
            Debug.Log("Starting client");
            NetworkManager.Singleton.StartClient();
            Hide();
        }

        void Hide() => gameObject.SetActive(false);
    }
}