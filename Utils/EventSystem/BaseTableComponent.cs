using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace CyberSpeed.CardsMatchGame
{
    public abstract class BaseTableComponent : MonoBehaviour, ITableComponent
    {

        [HideInInspector] public string tableId;

        [HideInInspector] public EventDispatcher dispatcher;

        [HideInInspector] public MonoBehaviour coroutineHandler;

        public void Initialize(TableInitPayload payload)
        {
            tableId = payload.tableId;
            dispatcher = payload.dispatcher;
            coroutineHandler = payload.coroutineHandler;
            Subscribe();
        }

        protected virtual void OnDestroy()
        {
            Unsubscribe();
        }

        protected virtual void Subscribe()
        {
        }

        protected virtual void Unsubscribe()
        {
        }
    }
}