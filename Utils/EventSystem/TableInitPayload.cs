using System.Collections;
using System.Collections.Generic;
using CyberSpeed.CardsMatchGame;
using UnityEngine;

namespace CyberSpeed.CardsMatchGame
{
    public class TableInitPayload
    {
        public string tableId;
        public EventDispatcher dispatcher;
        public MonoBehaviour coroutineHandler;
    }
}
