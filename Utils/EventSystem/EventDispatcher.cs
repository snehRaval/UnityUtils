namespace CyberSpeed.CardsMatchGame
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    public class EventDispatcher : MonoBehaviour
    {
        // Dictionary to store event listeners by event name
        private readonly Dictionary<string, List<Delegate>> eventListeners = new();

        // Subscribe to an event with or without a payload
        public void Subscribe(string eventName, Action listener)
        {
            if (!eventListeners.ContainsKey(eventName))
            {
                eventListeners[eventName] = new List<Delegate>();
            }

            eventListeners[eventName].Add(listener);
        }

        public void Subscribe<T>(string eventName, Action<T> listener)
        {
            if (!eventListeners.ContainsKey(eventName))
            {
                eventListeners[eventName] = new List<Delegate>();
            }

            eventListeners[eventName].Add(listener);
        }
        // Subscribe using only event type
        public void Subscribe<T>(Action<T> listener)
        {
            Subscribe(typeof(T).FullName, listener);
        }
        // Unsubscribe from an event with or without a payload
        public void Unsubscribe(string eventName, Action listener)
        {
            if (eventListeners.ContainsKey(eventName))
            {
                eventListeners[eventName].Remove(listener);
                if (eventListeners[eventName].Count == 0)
                {
                    eventListeners.Remove(eventName);
                }
            }
        }

        public void Unsubscribe<T>(string eventName, Action<T> listener)
        {
            if (eventListeners.ContainsKey(eventName))
            {
                eventListeners[eventName].Remove(listener);
                if (eventListeners[eventName].Count == 0)
                {
                    eventListeners.Remove(eventName);
                }
            }
        }

        // Unsubscribe using only event type
        public void Unsubscribe<T>(Action<T> listener)
        {
            Unsubscribe(typeof(T).FullName, listener);
        }
        // Unsubscribe all events associated with a MonoBehaviour
        public void UnsubscribeAll(MonoBehaviour subscriber)
        {
            // Collect event names that need modification
            var keysToModify = new List<string>();

            foreach (var key in eventListeners.Keys)
            {
                // Filter out delegates that belong to the subscriber
                var toRemove = eventListeners[key].FindAll(listener => listener.Target == subscriber);

                if (toRemove.Count > 0)
                {
                    keysToModify.Add(key);
                }
            }

            // Remove the collected delegates safely
            foreach (var key in keysToModify)
            {
                eventListeners[key].RemoveAll(listener => listener.Target == subscriber);

                if (eventListeners[key].Count == 0)
                {
                    eventListeners.Remove(key);
                }
            }
        }

        // Dispatch an event by name with or without a payload
        public void Dispatch(string eventName)
        {
            if (eventListeners.ContainsKey(eventName))
            {
                foreach (Delegate listener in eventListeners[eventName])
                {
                    ((Action) listener)?.Invoke();
                }
            }
            else
            {
                Debug.LogWarning($"No listeners found for event: {eventName}");
            }
        }

        public void Dispatch<T>(string eventName, T payload)
        {
            if (eventListeners.ContainsKey(eventName))
            {
                // Copy the list before iterating to prevent modification issues
                var listenersCopy = new List<Delegate>(eventListeners[eventName]);

                foreach (Delegate listener in listenersCopy)
                {
                    ((Action<T>) listener)?.Invoke(payload);
                }
            }
            else
            {
                Debug.LogWarning($"No listeners found for event: {eventName}");
            }
        }

        // Dispatch using only event type
        public void Dispatch<T>(T payload)
        {
            Dispatch(typeof(T).FullName, payload);
        }
        private void OnDestroy()
        {
            // Clean up listeners to avoid memory leaks
            eventListeners.Clear();
        }
    }
}