using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CyberSpeed.CardsMatchGame
{
    public class ExampleClass : BaseTableComponent
    {
        protected override void Subscribe()
        {
            dispatcher.Subscribe<int>(GameEvents.GAME_STARTED,OnGameStart);
            dispatcher.Subscribe<int>(GameEvents.GAME_OVER,OnGameOver);
        }
        protected override void Unsubscribe()
        {
            if(dispatcher == null) return;
            dispatcher.UnsubscribeAll(this);
        }
        
        private void OnGameStart(int obj)
        {
            throw new System.NotImplementedException();
        }
        private void OnGameOver(int obj)
        {
            throw new System.NotImplementedException();
        }

        private void GameScoreUpdate()
        {
            dispatcher.Dispatch(GameEvents.GAME_SCORE_UPDATE, new ScoreManager());
        }
    }
}
