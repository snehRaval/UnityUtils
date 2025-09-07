using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CyberSpeed.CardsMatchGame
{
    public interface ITableComponent
    {
        void Initialize(TableInitPayload payload);
    }
}