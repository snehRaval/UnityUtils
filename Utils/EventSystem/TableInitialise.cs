using System.Linq;
using UnityEngine;

namespace CyberSpeed.CardsMatchGame
{
    public class TableInitialise : MonoBehaviour
    {
        void Start()
        {
            InitializeTableComponents("tableID");
        }

        public void InitializeTableComponents(string tableId)
        {
            var tableComponents = GetComponentsInChildren<BaseTableComponent>(true).ToList();
            for (int i = 0; i < tableComponents.Count; i++)
            {
                tableComponents[i].Initialize(new TableInitPayload()
                {
                    coroutineHandler = GameManager.Instance,
                    tableId = tableId,
                    dispatcher = GameManager.Instance.eventDispatcher
                });
            }
        }
    }
}