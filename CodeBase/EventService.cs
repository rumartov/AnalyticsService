using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class EventService : MonoBehaviour
{
    [SerializeField] private string serverUrl = "https://your-analytics-server.com/events";
    [SerializeField] private float cooldownBeforeSend = 2.0f; 

    private List<EventData> eventQueue = new List<EventData>(); 
    private bool isCooldownActive = false;
    private bool isSending = false;

    private string eventStorageKey = "pendingEvents"; 

    private void Start()
    {
        LoadPendingEvents();
    }

    public void TrackEvent(string type, string data)
    {
        EventData newEvent = new EventData(type, data);
        eventQueue.Add(newEvent);
        
        if (!isCooldownActive)
        {
            StartCoroutine(CooldownAndSend());
        }
    }

    private IEnumerator CooldownAndSend()
    {
        isCooldownActive = true;
        
        yield return new WaitForSeconds(cooldownBeforeSend);
        
        if (eventQueue.Count > 0)
        {
            StartCoroutine(SendEvents());
        }

        isCooldownActive = false;
    }

    private IEnumerator SendEvents()
    {
        if (isSending) yield break;

        isSending = true;
        
        List<EventData> eventsToSend = new List<EventData>(eventQueue);
        string jsonPayload = JsonUtility.ToJson(new EventBatch(eventsToSend));
        
        using (UnityWebRequest request = new UnityWebRequest(serverUrl, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success && request.responseCode == 200)
            {
                eventQueue.Clear();
                SavePendingEvents();
            }
            else
            {
                SavePendingEvents();
                Debug.LogError($"Failed to send events: {request.error}");
            }
        }

        isSending = false;
    }

    private void SavePendingEvents()
    {
        string json = JsonUtility.ToJson(new EventBatch(eventQueue));
        PlayerPrefs.SetString(eventStorageKey, json);
        PlayerPrefs.Save();
    }

    private void LoadPendingEvents()
    {
        if (PlayerPrefs.HasKey(eventStorageKey))
        {
            string json = PlayerPrefs.GetString(eventStorageKey);
            EventBatch loadedEvents = JsonUtility.FromJson<EventBatch>(json);
            if (loadedEvents != null && loadedEvents.events != null)
            {
                eventQueue.AddRange(loadedEvents.events);
            }
        }
    }

    [Serializable]
    public class EventData
    {
        public string type;
        public string data;

        public EventData(string type, string data)
        {
            this.type = type;
            this.data = data;
        }
    }

    [Serializable]
    public class EventBatch
    {
        public List<EventData> events;

        public EventBatch(List<EventData> events)
        {
            this.events = events;
        }
    }

    private void OnApplicationQuit()
    {
        SavePendingEvents();
    }
}