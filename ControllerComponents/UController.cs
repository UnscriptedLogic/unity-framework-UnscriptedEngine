namespace UnscriptedEngine
{
    public abstract class UController : ULevelPawn
    {
        protected ULevelPawn possessedPawn;

        protected override void OnLevelStarted()
        {
            base.OnLevelStarted();

            possessedPawn = PossessPawn();

            OnPawnCreated += Controller_OnPawnCreated;
            OnPawnToBeDestroyed += Controller_OnPawnDestroyed;
        }

        private void Controller_OnPawnCreated(object sender, System.EventArgs e)
        {
            if (possessedPawn != null) return;

            possessedPawn = PossessPawn();
        }

        private void Controller_OnPawnDestroyed(object sender, System.EventArgs e)
        {
            ULevelPawn levelPawn = sender as ULevelPawn;
            if (levelPawn == possessedPawn)
            {
                possessedPawn = null;
                UnPossessPawn();
            }
        }

        protected override void OnLevelStopped()
        {
            OnPawnCreated -= Controller_OnPawnCreated;
            OnPawnToBeDestroyed -= Controller_OnPawnDestroyed;

            UnPossessPawn();

            base.OnLevelStopped();
        }

        protected void PauseGame() => GameMode.PauseGame();
        protected void ResumeGame() => GameMode.ResumeGame();

        protected virtual ULevelPawn PossessPawn() => null;
        protected virtual void UnPossessPawn() { }
    }

}