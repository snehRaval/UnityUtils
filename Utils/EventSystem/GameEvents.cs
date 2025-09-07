namespace CyberSpeed.CardsMatchGame
{
    public static class GameEvents
    {
        public const string GAME_STARTED = "game.started";
        public const string GAME_OVER = "game.over";
        public const string GAME_SAVED = "game.saved";
        public const string GAME_SCORE_UPDATE = "game.scoreIpdate";
        public const string GAME_CARD_MATCH = "game.cardMatchFound";
        public const string GAME_CARD_MISSMATCH = "game.cardMatchNotFound";
    }
    public struct GameStartedPayload { public int rows; public int cols; }
    public struct GameSavedPayload { public string key; }

    public struct GameOverPayload { public IScoreData finalScore; }
    public struct ScoreUpdatePayload { public IScoreData scoreData; }
   
}


